using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Text.RegularExpressions;
using System.IO;


namespace WordpressDrive
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {

        public SettingsWindow()
        {

            InitializeComponent();
            SystemTab.DataContext = Settings.Instance.SysSettings;

            this.DataContext = Settings.Instance;
            lbHostlist.SelectedIndex = 0;
            HostValues.DataContext = (Settings.HostSettings)lbHostlist.SelectedItem;

            Drive.ItemsSource = Utils.FreeDrives();

        }

        private string _Version;

        public string Version
        {
            get {
                if (string.IsNullOrWhiteSpace(_Version))
                {
                    System.Reflection.AssemblyName assemblyName = System.Reflection.Assembly.GetExecutingAssembly().GetName();
                    _Version = $"{assemblyName.Name} ({assemblyName.Version.ToString()})";
                }
                return _Version;
            }
            
        }


        private void Window_Closed(object sender, EventArgs e)
        {           
            Settings.SaveSettings();
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox lb = (sender as ListBox);
            Settings.HostSettings selItem = (Settings.HostSettings)lb.SelectedItem;
            if(selItem != null)
            {
                HostValues.DataContext = selItem;

                //statusText.Content = Utils.DecryptString(selItem.Password);
                statusText.Content = selItem.DisplayName;

            }
        }

        private void BtHostNew_Click(object sender, RoutedEventArgs e)
        {
            Settings.HostSettings newHost = new Settings.HostSettings(Settings.Instance.SysSettings)
            {
                DisplayName = "New Host",
            };

            Settings.Instance.HostsSettings.Add(newHost);
            lbHostlist.SelectedIndex = lbHostlist.Items.Count - 1;
        }

        private void BtHostDelete_Click(object sender, RoutedEventArgs e)
        {
            Settings.HostSettings host = (lbHostlist.SelectedItem as Settings.HostSettings);
            if (host == null)
            {
                Utils.Notify(Properties.Resources.please_select_item);
                return;
            }

            if(MessageBox.Show(Properties.Resources.do_you_want_delete, Properties.Resources.warning, MessageBoxButton.YesNo) == MessageBoxResult.No)
                return;

            if (Settings.Instance.HostsSettings.Remove(host))
                lbHostlist.SelectedIndex = 0;

        }

        private static readonly Regex _regex = new Regex("[^0-9.-]+"); //regex that matches disallowed text

        private void MaxDirectoryItems_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = _regex.IsMatch(e.Text);
        }

        private void BtOpenMainConfigDir_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(Utils.AppData().Item1))
                System.Diagnostics.Process.Start(Utils.AppData().Item1);
            else
                Utils.Notify(Properties.Resources.directory_not_exist, Utils.LOGLEVEL.ERROR);
        }

        private void BtOpenHostCacheDir_Click(object sender, RoutedEventArgs e)
        {
            if (HostValues.DataContext == null) return;
            Settings.HostSettings curHost = (Settings.HostSettings)HostValues.DataContext;
            string cacheDir = System.IO.Path.Combine(Utils.AppData().Item1, curHost.Id);

            if(Directory.Exists(cacheDir))
                System.Diagnostics.Process.Start(cacheDir);
            else
                Utils.Notify(Properties.Resources.directory_not_exist);

        }
    }
}
