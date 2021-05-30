using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.ComponentModel;
using System.Globalization;


namespace WordpressDrive
{
    public class Settings
    {

        private ObservableCollection<HostSettings> _HostsSettings = new ObservableCollection<HostSettings>();
        
        private readonly static string _ConfigFile;
        private static Settings _instance = null;
        public SystemSettings SysSettings;

        public ObservableCollection<HostSettings> HostsSettings { get { return _HostsSettings; } }

        public static Settings Instance { get {
                if (_instance == null) _instance = new Settings();
                return _instance;
            }
        }

        static Settings()
        {
           _ConfigFile = System.IO.Path.Combine(Utils.AppData().Item1, "settings.json");

            if (File.Exists(_ConfigFile))
                _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(_ConfigFile));
            else
            {
                string sysConfig = Path.Combine(Utils.AppData().Item3, "settings.json");
                if (File.Exists(sysConfig))
                    _instance = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(sysConfig));
                else
                    _instance = new Settings();
            }

            if (_instance.SysSettings == null) _instance.SysSettings = new Settings.SystemSettings();
        }

        private Settings() {
            _HostsSettings.CollectionChanged += _HostsSettings_CollectionChanged;
        }

        private void _HostsSettings_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
           
            (App.Current as WordpressDrive.App).UpdateMenu();
        }

        public static void SaveSettings()
        {
            File.WriteAllText(_ConfigFile, JsonConvert.SerializeObject(
                Settings.Instance, Formatting.Indented /*,
                new JsonSerializerSettings()
                {
                    ContractResolver = new IgnorePropertiesResolver(new[]
                    { "clearPassword", "isMounted", "Password"}
                    )
                }*/
             ));
        }

        public class HostSettings
        {
            private string _DisplayName;
            public string DisplayName {
                get { return _DisplayName; }
                set {
                    _DisplayName = value;
                    System.Windows.Data.CollectionViewSource.GetDefaultView(Settings.Instance.HostsSettings).Refresh();
                }
            }
            public string HostUrl { get; set; }
            public string Username { get; set; }
            public string Drive { get; set; }
            public bool AnonymousLogin { get; set; }
            public string Password { get; set; }
            public int MaxItemsPerDirectory { get; set; }
            public bool CacheAttachments { get; set; }
            public bool CacheOthers { get; set; }
            public bool ShowSyncDlg { get; set; } = true;
            public bool AutoCloseSyncDlg { get; set; } = false;

            [JsonIgnore]
            public bool CanModifyAttachment { get; set; } = false;

            [JsonIgnore]
            public bool IsDefaultHost
            {
                get {
                    return Settings.Instance.SysSettings.DefaultHostId == Id;
                }
                set {
                    Settings.Instance.SysSettings.DefaultHostId = Id;
                }
            }


            private string _id=null;
            public string Id
            {
                get {
                    if (_id == null) _id = Utils.GetId();
                    return _id;
                }
                set { _id = value; }
            }

            [JsonIgnore]
            public string PasswordDec
            {
                get
                {
                    if (string.IsNullOrEmpty(Password))
                        return null;
                    else
                        return Utils.DecryptString(Password);
                }
            }

            [JsonIgnore]
            public bool IsMounted { get; set; } = false;

            const string _clearPassword = "************";

            [JsonIgnore]
            public string ClearPassword { 
                get { return _clearPassword; }
                set
                {
                    if (string.IsNullOrEmpty(value))
                        Password = "";
                    else if (value != _clearPassword)
                        Password = Utils.EncryptString(Utils.ToSecureString(value));
                }
            }

            public HostSettings()
            {
            }

            public HostSettings(SystemSettings SysSettings)
            {
                Utils.CopyProperties<SystemSettings, HostSettings>(SysSettings, this);
             }

            public override string ToString()
            {
                return DisplayName;
            }

            public HostSettings Clone()
            {
                IgnorePropertiesResolver ignore = new IgnorePropertiesResolver(new[]
                { "clearPassword", "isMounted", "Password"}
                );

                var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace/*, ContractResolver = ignore*/ };
                var serializeSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore /*, ContractResolver = ignore*/ };
                return JsonConvert.DeserializeObject<HostSettings>(JsonConvert.SerializeObject(this, serializeSettings), deserializeSettings);

            }

            public void Validate()
            {

                if (string.IsNullOrWhiteSpace(DisplayName))
                    throw new AppException<SettingValidationException>("Display Name must not be Empty!");
            }
        }

        public class SystemSettings
        {
            public int RequestTimeout { get; set; } = 100000;
            public int RequestRetries { get; set; } = 4;
            public int MaxItemsPerDirectory { get; set; } = 500;
            public bool CacheAttachments { get; set; } = true;
            public bool CacheOthers { get; set; } = false;
            public bool AnonymousLogin { get; set; } = false;
            public bool ShowSyncDlg { get; set; } = true;
            public bool AutoCloseSyncDlg { get; set; } = false;
            public bool ShowConnectMsg { get; set; } = true;
            public bool AcceptAllSSLCerts { get; set; } = false;
            public string UserAgent { get; set; } = "Wordpress Drive API Client";

            private string _defaultHostId=null;
            public string DefaultHostId
            {
                get {
                    if (Settings.Instance.HostsSettings == null || Settings.Instance.HostsSettings.Count == 0) return null;
                    if(_defaultHostId == null)
                        return Settings.Instance.HostsSettings[0].Id;
                    else
                        return _defaultHostId;
                }
                set {
                    _defaultHostId = value;
                }
            }


            private string _currentLangCode = "en";
            public string CurrentLangCode
            {
                get { return _currentLangCode; }
                set {
                    CultureInfo ci = new CultureInfo(value);
                    if (!ci.Equals(Properties.Resources.Culture))
                        Properties.Resources.Culture = ci;
                    _currentLangCode = value;
                }
            }

            [JsonIgnore]
            public CultureInfo CurrentCultureInfo
            {
                get { return new CultureInfo(_currentLangCode); }
                set { CurrentLangCode = value.Name; }
            }


            private ObservableCollection<CultureInfo> _availableLanguages=null;
            [JsonIgnore]
            public ObservableCollection<CultureInfo> AvailableLanguages
            {
                get {
                    if(_availableLanguages == null)
                    {
                        _availableLanguages = new ObservableCollection<System.Globalization.CultureInfo>();
                        var cultures = Utils.GetAvailableCultures();
                        foreach (CultureInfo culture in cultures)
                            _availableLanguages.Add(culture);
                    }
                    return _availableLanguages;
                }
            }

            public Settings.HostSettings GetHostById(string hostId)
            {
                Settings.HostSettings ret = null;
                foreach(Settings.HostSettings host in Settings.Instance.HostsSettings)
                {
                    if(host.Id == hostId)
                    {
                        ret = host;
                        break;
                    }
                }
                return ret;
            }

            public SystemSettings()
            {
            }
        }


        //short helper class to ignore some properties from serialization
        public class IgnorePropertiesResolver : DefaultContractResolver
        {
            private readonly HashSet<string> ignoreProps;
            public IgnorePropertiesResolver(IEnumerable<string> propNamesToIgnore)
            {
                this.ignoreProps = new HashSet<string>(propNamesToIgnore);
            }

            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                JsonProperty property = base.CreateProperty(member, memberSerialization);
                if (this.ignoreProps.Contains(property.PropertyName))
                {
                    property.ShouldSerialize = _ => false;
                }
                return property;
            }
        }
    }

    public class CultureinfoToStringConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                CultureInfo ci = (CultureInfo)value;
                //return ci.NativeName + " (" + ci.EnglishName + " [" + ci.TwoLetterISOLanguageName + "])";
                return ci.NativeName + " (" + ci.EnglishName + " [" + ci.Name + "])";
            }
            catch
            {
                return string.Empty;
            }
        }

        // No need to implement converting back on a one-way binding 
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
