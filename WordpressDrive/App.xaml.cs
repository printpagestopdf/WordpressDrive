using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Hardcodet.Wpf.TaskbarNotification;
using System.Globalization;

namespace WordpressDrive
{
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application
    {
        private TaskbarIcon _notifyIcon;



        public TaskbarIcon NotifyIcon { get { return _notifyIcon; } }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            
            try
            {
                WordpressDrive.Properties.Resources.Culture = Settings.Instance.SysSettings.CurrentCultureInfo;
            }
            catch
            {
                WordpressDrive.Properties.Resources.Culture = CultureInfo.CurrentUICulture;
            }

            //create the notifyicon (it's a resource declared in NotifyIconResources.xaml
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");
            BuildMenu();

            CheckHostslist();
        }

        protected void CheckHostslist()
        {
            if(Settings.Instance.HostsSettings.Count == 0)
            {
                Application.Current.MainWindow = new SettingsWindow();
                Application.Current.MainWindow.Show();
            }
        }

        protected void BuildMenu()
        {
            foreach(Settings.HostSettings host in Settings.Instance.HostsSettings)
            {
                System.Windows.Controls.MenuItem mi = new System.Windows.Controls.MenuItem()
                {
                    Header = host.DisplayName,
                    Command = (_notifyIcon.DataContext as NotifyIconViewModel).MountWPHost,
                    CommandParameter = host,
                };
                _notifyIcon.ContextMenu.Items.Insert(0,mi);
            }

        }

        public void UpdateMenu()
        {
            if (_notifyIcon == null ||
                !_notifyIcon.Dispatcher.Thread.Equals(System.Threading.Thread.CurrentThread)) return;

            for (int i = _notifyIcon.ContextMenu.Items.Count - 1; i >= 0; i--)
            {
                if (!(_notifyIcon.ContextMenu.Items[i] is System.Windows.Controls.MenuItem)) continue;
                System.Windows.Controls.MenuItem mi = _notifyIcon.ContextMenu.Items[i] as System.Windows.Controls.MenuItem;
                if (mi.CommandParameter != null && (mi.CommandParameter is Settings.HostSettings))
                    _notifyIcon.ContextMenu.Items.RemoveAt(i);
            }

            BuildMenu();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Settings.SaveSettings();
            _notifyIcon.Dispose(); //the icon would clean up automatically, but this is cleaner
            base.OnExit(e);
        }

    }
}
