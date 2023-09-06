using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Mullvad_Servers_Pinger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public record Relay(string ipv4_addr_in, string country_name, string city_name, string hostname, string type);
    public record PingResponse(string address, string country, string city, long time, string hostname, bool bridge = false);
    public partial class MainWindow : Window
    {
        private string _fileResults = "";
        private double MaxPing = 9999;
        public MainWindow()
        {
            InitializeComponent();
            infoLbl.Visibility = Visibility.Hidden;
            progress.Visibility = Visibility.Hidden;
        }

        private void FindBtn_Copy_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void FindBtn_Click(object sender, RoutedEventArgs e)
        {
            await Process();
        }
        private async Task Process()
        {
            max_time_slider.Visibility = Visibility.Hidden;
            checkBridges.Visibility = Visibility.Hidden;
            max_time.Visibility = Visibility.Hidden;
            FindBtn.Visibility = Visibility.Hidden;
            ignoreLbl.Visibility = Visibility.Hidden;
            FindBtn_Copy.Visibility = Visibility.Hidden;
            //
            progress.Visibility = Visibility.Visible;
            progress.Opacity = 100;
            finalexit.Visibility = Visibility.Visible;
            infoLbl.Visibility= Visibility.Visible;
            HttpClient httpClient = new HttpClient();
            HttpResponseMessage? response = null;
            try
            {

                response = await httpClient.GetAsync(@"https://api.mullvad.net/www/relays/all/");
            }
            catch
            {
                try
                {

                    response = await httpClient.GetAsync(@"https://pastebin.com/raw/jgHrea3N");
                }
                catch
                {
                    infoLbl.Content = "Failed to fetch relays live... loading from local file";
                    MessageBox.Show("Failed to Fetch from live relays","Network Connectivity Error",MessageBoxButton.OK,MessageBoxImage.Error);
                    Close();
                }
            }
            var responseString = await response.Content.ReadAsStringAsync();
            List<Relay>? relays = JsonConvert.DeserializeObject<List<Relay>>(responseString);
            List<PingResponse> pingResponses = new List<PingResponse>();
            infoLbl.Content = $"Got {relays.Count} relays!";
            int i = 1;
            progress.Maximum = relays.Count;
            foreach (var relay in relays)
            {

                progress.Value = i;
                await Task.Delay(100); //Don't want to flood my fav VPN ❤️
                infoLbl.Content = $"Processing relays";
                await Task.Run(() =>
                {
                    using (var ping = new Ping())
                    {
                        try
                        {

                            var pingResponse = ping.Send(relay.ipv4_addr_in, 300);
                            if (pingResponse.Status == IPStatus.Success)
                                pingResponses.Add(new(relay.ipv4_addr_in, relay.country_name, relay.city_name, pingResponse.RoundtripTime,relay.hostname, relay.type == "bridge" ? true : false));
                        }
                        catch
                        {

                        }
                    }

                });
                i++;
            }
            infoLbl.Foreground = Brushes.LimeGreen;
            infoLbl.Content ="Finished Processing!";
            if (checkBridges.IsChecked == true)
            {
                _fileResults += "= Bridges = \n";
                pingResponses.SkipWhile(x => x.time >= MaxPing).Where(x => x.bridge).OrderBy(x => x.time).ToList().ForEach(resp => { _fileResults += $"{resp.hostname} {resp.country} {resp.city}  [{resp.time}ms]\n"; pingResponses.Remove(resp); });
            }
            _fileResults += "= Normal Relays =\n";
            pingResponses.SkipWhile(x => x.time >= MaxPing).OrderBy(x => x.time).ToList().ForEach(resp => _fileResults += $"{ resp.hostname} {resp.country} {resp.city} [{resp.time}ms]\n");
            _fileResults += "[+] ========== End ========== [+]\n";
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = "results.txt";
            saveFileDialog.Filter = "Text File|*.txt";
            saveFileDialog.Title = "Save Your Results to a File";
            saveFileDialog.ShowDialog();
            if (!string.IsNullOrEmpty(saveFileDialog.FileName) && saveFileDialog.FileName != "results.txt")
            {
                File.WriteAllText(saveFileDialog.FileName, _fileResults);
                var result = MessageBox.Show("Would you like to view the results file?","Results",MessageBoxButton.YesNo, MessageBoxImage.Question);
                if(result == MessageBoxResult.Yes)
                    OpenFile(saveFileDialog.FileName);
            }
            
           
        }
        private void OpenFile(string path)
        {
            using Process fileOpener = new Process();
            fileOpener.StartInfo.FileName = "explorer";
            fileOpener.StartInfo.Arguments = "\"" + path + "\"";
            fileOpener.Start();
        }

        private void max_time_slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
           
        }

        private void max_time_slider_ValueChanged_1(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            max_time.Content = $"{e.NewValue.ToString("f0")}ms";
            switch (e.NewValue)
            {
                case 0:
                    max_time.Foreground = Brushes.White;
                    max_time.Content = "Don't Ignore";
                    MaxPing = 9999;
                    break;
                case > 600:
                    max_time.Foreground = Brushes.Red;
                    break;
                case > 300:
                    max_time.Foreground = Brushes.Orange;
                    break;
                case < 300:
                    max_time.Foreground = Brushes.LimeGreen;
                    break;

            }
            MaxPing = e.NewValue;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }
    }
}
