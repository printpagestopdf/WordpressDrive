using System;
using System.Windows;
using System.Windows.Input;
using System.Threading.Tasks;
using Hardcodet.Wpf.TaskbarNotification;
using System.ComponentModel;

using System.Runtime.CompilerServices;

namespace WordpressDrive
{
    /// <summary>
    /// Provides bindable properties and commands for the NotifyIcon. In this sample, the
    /// view model is assigned to the NotifyIcon in XAML. Alternatively, the startup routing
    /// in App.xaml.cs could have created this view model, and assigned it to the NotifyIcon.
    /// </summary>
    public class NotifyIconViewModel: INotifyPropertyChanged
    {
        /// <summary>
        /// Shows a window, if none is already open.
        /// </summary>
        /// 
        public ICommand MountWPHost
        {
            get
            {
                
                return new DelegateCommand
                {
                    
                    CanExecuteFunc = (parameter) => (parameter == null || !(parameter as Settings.HostSettings).IsMounted),
                    CommandAction = (parameter) =>
                    {
                        MountHost((parameter as Settings.HostSettings));
                    }
                };
            }
        }

        protected void MountHost(Settings.HostSettings host)
        {
            Task.Run(() => { WPWinFspService.Instance.MountHost(host); }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    if (t.Exception.InnerException is AppException<AuthCancelledException>)
                        Utils.Notify(t.Exception.InnerException.Message);
                    else if (t.Exception.InnerException is AppException<AuthFailedException>)
                        Utils.Notify(t.Exception.InnerException.Message, Utils.LOGLEVEL.ERROR);
                }
                else
                {
                    if (host.IsMounted)
                    {
                        Icon = "/Resources/SystemTrayAppConnected.ico";
                    }
                    else
                        Utils.Notify(String.Format(Properties.Resources.Unknow_connect_error, host.DisplayName), Utils.LOGLEVEL.ERROR);
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        /// <summary>
        /// Executed on icon double click.
        /// </summary>
        public ICommand ShowSettingsWindowCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = (parameter) => Application.Current.MainWindow == null,
                    CommandAction = (parameter) =>
                    {
                        Application.Current.MainWindow = new SettingsWindow();
                        Application.Current.MainWindow.Show();
                    }
                };
            }
        }

        /// <summary>
        /// Executed on icon double click.
        /// </summary>
        public ICommand OnDoubleClickCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CanExecuteFunc = (parameter) => Settings.Instance.SysSettings.DefaultHostId  != null,
                    CommandAction = (parameter) =>
                    {
                        Settings.HostSettings defHost = Settings.Instance.SysSettings.GetHostById(Settings.Instance.SysSettings.DefaultHostId);
                        if (defHost != null)
                        {
                            if(WPWinFspService.Instance.ConnectedTo != null && defHost.Id == WPWinFspService.Instance.ConnectedTo.Id)
                                UmountHost();
                            else
                                MountHost(defHost);
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Hides the main window. This command is only enabled if a window is open.
        /// </summary>
        public ICommand HideSettingsWindowCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (parameter) => Application.Current.MainWindow.Close(),
                    CanExecuteFunc = (parameter) => Application.Current.MainWindow != null
                };
            }
        }


        protected void UmountHost()
        {
            WPWinFspService.Instance.UmountHost();
            if (WPWinFspService.Instance.IsConnected)
                Icon = "/Resources/SystemTrayAppConnected.ico";
            else
                Icon = "/Resources/SystemTrayApp.ico";
        }

        /// <summary>
        /// Umount WinFsp Drive.
        /// </summary>
        public ICommand UmountDriveCommand
        {
            get
            {
                return new DelegateCommand
                {
                    CommandAction = (parameter) => {
                        UmountHost();
                    },
                    CanExecuteFunc = (parameter) => WPWinFspService.Instance.IsConnected,
                };
            }
        }

        /// <summary>
        /// Umount WinFsp Drive.
        /// </summary>
        public ICommand SynchronizeCacheCommand
        {
            get
            {
                return new DelegateCommand
                {
                    //CommandAction = () => new WPWinFspService().Run(),
                    CommandAction = (parameter) => {
                        WPWinFspService.Instance.CurrentFileSystem().Synchronize();
                    },
                    CanExecuteFunc = (parameter) => WPWinFspService.Instance.IsConnected && WPWinFspService.Instance.CurrentFileSystem().SyncSize > 0,
                };
            }
        }


        /// <summary>
        /// Shuts down the application.
        /// </summary>
        public ICommand ExitApplicationCommand
        {
            get
            {
                return new DelegateCommand
                { CommandAction = (parameter) => {
                    WPWinFspService.Instance.UmountHost();
                    Application.Current.Shutdown();
                    },
                };
            }
        }

        private string _Icon= "/Resources/SystemTrayApp.ico";
        public string Icon
        {
            get
            {
                return _Icon;
            }
            set
            {
                if(_Icon != value)
                {
                    _Icon = value;
                    NotifyPropertyChanged(); 
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.
        // The CallerMemberName attribute that is applied to the optional propertyName
        // parameter causes the property name of the caller to be substituted as an argument.
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
             PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    /// <summary>
    /// Simplistic delegate command
    /// </summary>
    public class DelegateCommand : ICommand
    {
        public Action<object> CommandAction { get; set; }
        public Func<object,bool> CanExecuteFunc { get; set; }

        public void Execute(object parameter)
        {
            CommandAction(parameter);
        }

        public bool CanExecute(object parameter)
        {
            return CanExecuteFunc == null  || CanExecuteFunc(parameter);
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
