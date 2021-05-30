
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using Fsp;

namespace WordpressDrive
{
    public class WPWinFspService:Service, INotifyPropertyChanged
    {

        private Settings.HostSettings _curHost;

        private static WPWinFspService _instance=null;

        public static WPWinFspService Instance
        {
            get {
                if (_instance == null)
                    _instance = new WPWinFspService();

                return _instance;
            }
        }


        public WPWinFspService() : base("WPWinFspService")
        {
        }


        public void MountHost(Settings.HostSettings curHost)
        {
            WPWinFs fs = new WPWinFs(curHost);
 
            if (_isConnected) UmountHost();
            this._curHost = curHost;

            InitHost(fs);

            NotifyPropertyChanged("isConnected");
            NotifyPropertyChanged("ConnectedTo");

            if(Settings.Instance.SysSettings.ShowConnectMsg)
                Utils.Notify(String.Format(Properties.Resources.successfully_connect_msg,curHost.DisplayName), Utils.LOGLEVEL.INFO,3000);
        }

        internal WPWinFs CurrentFileSystem()
        {
            if (_Host == null) return null;
            return (WPWinFs)_Host.FileSystem();
        }

        private void InitHost(WPWinFs fs)
        {
            try
            {
                String DebugLogFile = null;
                UInt32 DebugFlags = 0;
                String VolumePrefix = $"\\Wordpress\\{Utils.GetSafeFilename(this._curHost.DisplayName)}";
                String MountPoint;
                IntPtr DebugLogHandle = (IntPtr)(-1);
                FileSystemHost Host = null;


                if (!string.IsNullOrWhiteSpace(_curHost.Drive) && Utils.IsDriveFree(_curHost.Drive))
                    MountPoint = _curHost.Drive;
                else
                    MountPoint = Utils.FirstFreeDrive(true);

                if (null != DebugLogFile)
                    if (0 > FileSystemHost.SetDebugLogFile(DebugLogFile))
                        throw new Exception("cannot open debug log file");

                Host = new FileSystemHost(fs) {
                    Prefix = VolumePrefix,
                    FileSystemName = "WordpressFS",
                };
                if (0 > Host.Mount(MountPoint, null, true, DebugFlags))
                    throw new IOException("cannot mount file system");

                if (_Host != null)
                {
                    _Host.Dispose();
                    _Host = null;
                }
                _Host = Host;
                _isConnected = true;
                this._curHost.IsMounted = true;

            }
            catch (Exception ex)
            {
                Log(EVENTLOG_ERROR_TYPE,  ex.Message);
                throw;
            }
        }

    public void UmountHost()
        {
            if (_Host != null)
            {
                CurrentFileSystem().Synchronize();

                _Host.Unmount();
                _Host.Dispose();
            }
            _isConnected = false;
            if(this._curHost != null)
                this._curHost.IsMounted = false;

            NotifyPropertyChanged("isConnected");
            NotifyPropertyChanged("ConnectedTo");

            System.Threading.Thread.Sleep(200);

        }


        protected override void OnStop()
        {
            _Host.Unmount();
            _isConnected = false;
            //_Host = null;
        }


        private FileSystemHost _Host;
        private bool _isConnected=false;

        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
        }

        public Settings.HostSettings ConnectedTo
        {
            get
            {
                if (!IsConnected || _curHost == null)
                    return null;
                else
                    return _curHost;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class HostTypeToStringConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value == null || !(value is Settings.HostSettings))
                    return Properties.Resources.not_connected;

                return (value as Settings.HostSettings).DisplayName;
            }
            catch
            {
                return Properties.Resources.not_connected;
            }
        }

        // No need to implement converting back on a one-way binding 
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


