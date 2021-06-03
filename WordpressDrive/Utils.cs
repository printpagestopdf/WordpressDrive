using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Collections;
using System.IO;
using System.Security;
using Newtonsoft.Json;
using System.Xml.Serialization;
using System.Threading;
using System.Globalization;
using System.Resources;

namespace WordpressDrive
{
    class Utils
    {
        public enum LOGLEVEL
        {
            INFO,
            ERROR,
            DEBUG,
            EXCEPTION,
            SECTION,
        }

        public static void Log(object txt, LOGLEVEL logLevel=LOGLEVEL.INFO)
        {
            if(logLevel == LOGLEVEL.SECTION)
            {
                System.Diagnostics.Debug.WriteLine($"================================{txt}=============================");
                return;
            }
            if (txt is string)
                System.Diagnostics.Debug.WriteLine(txt);
            else
                System.Diagnostics.Debug.WriteLine(JsonConvert.SerializeObject(txt, Formatting.Indented));
        }

        public static string GetSafeFilename(string pathName, int pathlength=0)
        {
            StringBuilder strPath = new StringBuilder(pathName);
            strPath.Replace('"', '\'');
            strPath.Replace('/', '-');
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                strPath.Replace(c, '_');
            }
            string safeFilename = strPath.ToString();
            //string safeFilename = string.Join("_", pathName.Split(Path.GetInvalidFileNameChars()));

            int overSize = Utils.MaxPath - ( safeFilename.Length + pathlength);

            if(overSize < 0)
            {
                string basename = Path.GetFileNameWithoutExtension(safeFilename);
                string ext = Path.GetExtension(Path.GetFileNameWithoutExtension(safeFilename));
                basename.Substring(0, basename.Length - (Math.Abs(overSize) + 3));
                safeFilename = basename.Substring(0, basename.Length - (Math.Abs(overSize) + 3)) + "..." + ext;
            }

            return safeFilename;

        }

        public static string GetCookieHeader(CookieCollection cookieCol)
        {
            string ret = "";
            foreach (Cookie cookie in cookieCol)
                ret += cookie.Name + "=" + cookie.Value + ";";

            return ret.TrimEnd(new Char[] { ';' });
        }

        public static CookieCollection GetAllCookiesFromHeader(string strHeader, string strHost)
        {
            ArrayList al = new ArrayList();
            CookieCollection cc = new CookieCollection();
            if (strHeader != string.Empty)
            {
                al = ConvertCookieHeaderToArrayList(strHeader);
                cc = ConvertCookieArraysToCookieCollection(al, strHost);
            }
            return cc;
        }


        private static ArrayList ConvertCookieHeaderToArrayList(string strCookHeader)
        {
            strCookHeader = strCookHeader.Replace("\r", "");
            strCookHeader = strCookHeader.Replace("\n", "");
            string[] strCookTemp = strCookHeader.Split(',');
            ArrayList al = new ArrayList();
            int i = 0;
            int n = strCookTemp.Length;
            while (i < n)
            {
                if (strCookTemp[i].IndexOf("expires=", StringComparison.OrdinalIgnoreCase) > 0)
                {
                    al.Add(strCookTemp[i] + "," + strCookTemp[i + 1]);
                    i = i + 1;
                }
                else
                {
                    al.Add(strCookTemp[i]);
                }
                i = i + 1;
            }
            return al;
        }


        private static CookieCollection ConvertCookieArraysToCookieCollection(ArrayList al, string strHost)
        {
            CookieCollection cc = new CookieCollection();

            int alcount = al.Count;
            string strEachCook;
            string[] strEachCookParts;
            for (int i = 0; i < alcount; i++)
            {
                strEachCook = al[i].ToString();
                strEachCookParts = strEachCook.Split(';');
                int intEachCookPartsCount = strEachCookParts.Length;
                string strCNameAndCValue = string.Empty;
                string strPNameAndPValue = string.Empty;
                string strDNameAndDValue = string.Empty;
                string[] NameValuePairTemp;
                Cookie cookTemp = new Cookie();

                for (int j = 0; j < intEachCookPartsCount; j++)
                {
                    if (j == 0)
                    {
                        strCNameAndCValue = strEachCookParts[j];
                        if (strCNameAndCValue != string.Empty)
                        {
                            int firstEqual = strCNameAndCValue.IndexOf("=");
                            string firstName = strCNameAndCValue.Substring(0, firstEqual);
                            string allValue = strCNameAndCValue.Substring(firstEqual + 1, strCNameAndCValue.Length - (firstEqual + 1));

                            if(cc[firstName] != null && cc[firstName].Value == allValue) //cookie already exists
                                goto OUTERLOOP;

                            cookTemp.Name = firstName;
                            cookTemp.Value = allValue;
                        }
                        continue;
                    }
                    if (strEachCookParts[j].IndexOf("path", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');
                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                cookTemp.Path = NameValuePairTemp[1];
                            }
                            else
                            {
                                cookTemp.Path = "/";
                            }
                        }
                        continue;
                    }

                    if (strEachCookParts[j].IndexOf("domain", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        strPNameAndPValue = strEachCookParts[j];
                        if (strPNameAndPValue != string.Empty)
                        {
                            NameValuePairTemp = strPNameAndPValue.Split('=');

                            if (NameValuePairTemp[1] != string.Empty)
                            {
                                cookTemp.Domain = NameValuePairTemp[1];
                            }
                            else
                            {
                                cookTemp.Domain = strHost;
                            }
                        }
                        continue;
                    }
                }

                if (cookTemp.Path == string.Empty)
                {
                    cookTemp.Path = "/";
                }
                if (cookTemp.Domain == string.Empty)
                {
                    cookTemp.Domain = strHost;
                }
                cc.Add(cookTemp);

            OUTERLOOP:;
            }
            return cc;
        }

        static readonly byte[] entropy = System.Text.Encoding.Unicode.GetBytes("{057A7078-5468-4EBB-87AE-399595FDABD7}");

        public static string EncryptString(System.Security.SecureString input)
        {
            byte[] encryptedData = System.Security.Cryptography.ProtectedData.Protect(
                System.Text.Encoding.Unicode.GetBytes(ToInsecureString(input)),
                entropy,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        public static string DecryptString(string encryptedData)
        {
            if (string.IsNullOrWhiteSpace(encryptedData)) return null;

            try
            {
                byte[] decryptedData = System.Security.Cryptography.ProtectedData.Unprotect(
                    Convert.FromBase64String(encryptedData),
                    entropy,
                    System.Security.Cryptography.DataProtectionScope.CurrentUser);
                return System.Text.Encoding.Unicode.GetString(decryptedData);
            }
            catch
            {
                return null;
            }
        }


        public static SecureString ToSecureString(string input)
        {
            SecureString secure = new SecureString();
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        public static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            IntPtr ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }

        public static IEnumerable<string> FreeDrives()
        {
            IEnumerable<string> drives = Enumerable.Range('C', 'Z' - 'C' + 1).Select(i => (Char)i + ":")
                    .Except(DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", "")));

            return drives;

        }

        public static string FirstFreeDrive(bool last=false)
        {
            if (last)
                return FreeDrives().Last<string>();
            else
                return FreeDrives().First<string>();
        }

        public static bool IsDriveFree(string drive)
        {
            return FreeDrives().Contains<string>(drive);
        }

        private static int _max_path = 259; //.NET and WinFsp seems to have hard limitation

        public static int MaxPath
        {
            get {
                if (_max_path < 0)
                {
                    
                System.Reflection.FieldInfo maxPathField = typeof(Path).GetField("MaxPath",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.GetField |
                    System.Reflection.BindingFlags.NonPublic);

                    // invoke the field gettor, which returns 260
                    _max_path = (int)maxPathField.GetValue(null);
                    //the NUL terminator is part of MAX_PATH https://msdn.microsoft.com/en-us/library/aa365247.aspx#maxpath
                    _max_path--; //So decrease by 1
                }
                
                return _max_path;
            }
        }

        static public T CleanJson<T>(string jsonData)
        {
            T m=default(T);
            StringBuilder json =new StringBuilder( jsonData.Replace("\t", "").Replace("\r\n", ""));
            var loop = true;
            do
            {
                try
                {
                    m = JsonConvert.DeserializeObject<T>(json.ToString());
                    loop = false;
                }
                catch (JsonReaderException ex)
                {
                    var position = ex.LinePosition;
                    //var invalidChar = json.Substring(position - 2, 2);
                    //invalidChar = invalidChar.Replace("\"", "'");
                    switch(json[position - 1])
                    {
                        case '"':
                            json[position - 1] = '\''; ;
                            break;
                        case '`':
                        case '\'':
                            json.Insert(position - 1, '\\');
                            break;

                        default:
                            json[position - 1] = '_';
                            break;
                    }
                    
                    //json = $"{json.Substring(0, position - 1)}_{json.Substring(position)}";
                }
            } while (loop);
            return m;// JsonConvert.DeserializeObject<T>(json);
        }


        public static T CreateDeepCopy<T>(T source)
        {
            // Don't serialize a null object, simply return the default for that object
            if (source == null)
            {
                return default(T);
            }

            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            var serializeSettings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source, serializeSettings), deserializeSettings);
        }

        public static string GetId()
        {
            Thread.Sleep(1);//make everything unique while looping
            long ticks = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0))).TotalMilliseconds;//EPOCH
            char[] baseChars = new char[] { '0','1','2','3','4','5','6','7','8','9',
            'A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z',
            'a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x'};

            int i = 32;
            char[] buffer = new char[i];
            int targetBase = baseChars.Length;

            do
            {
                buffer[--i] = baseChars[ticks % targetBase];
                ticks = ticks / targetBase;
            }
            while (ticks > 0);

            char[] result = new char[32 - i];
            Array.Copy(buffer, i, result, 0, 32 - i);

            return new string(result);
        }

        private static Tuple<string, string, string, string> _AppData = null;
        public static Tuple<string,string,string,string> AppData()
        {
            if (_AppData != null) return _AppData;
            string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string ExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            //string AppName = System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location);
            string AppName = System.IO.Path.GetFileNameWithoutExtension(ExePath);
            string ExeDirectory = Path.GetDirectoryName(ExePath);
            string ConfigPath = System.IO.Path.Combine(AppDataPath, AppName);
            Directory.CreateDirectory(ConfigPath);

            _AppData= Tuple.Create<string, string, string, string>(ConfigPath, AppName, ExeDirectory, AppDataPath);
            return _AppData;

        }

        public static Stream StreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }


        public static List<Tuple<DateTime,DateTime>> WeeksOfMonth(DateTime dt)
        {
            DateTime from = new DateTime(dt.Year, dt.Month, 1, 0, 0, 0);
            List<Tuple<DateTime, DateTime>> result = new List<Tuple<DateTime, DateTime>>();
            int month = from.Month;
            int daysInWeek = 7 - (int)from.DayOfWeek;

            while (true)
            {
                if (from.Month != month) break;
                DateTime to = from.AddDays(daysInWeek);
                if (to.Month != month)
                {
                    to = new DateTime(from.Year, from.Month, DateTime.DaysInMonth(from.Year, from.Month));
                }
                result.Add(Tuple.Create<DateTime, DateTime>(from, to));
                daysInWeek = 6;
                from = to.AddDays(1);
            }

            return result;
            
        }

        public static string[] GetFilenameParts(string path,int maxLength=5)
        {
            int pos = path.LastIndexOf('.');
            if (pos <= 0 || path.Length - pos > maxLength || path.Length - pos <= 1) return new string[] {path,"" };
            return new string[] { path.Substring(0,pos - 1), path.Substring(pos) } ;
        }

        public static Target CopyProperties<Source, Target>(Source source, Target target)
        {
            foreach (var sProp in source.GetType().GetProperties())
            {
                bool isMatched = target.GetType().GetProperties().Any(tProp => tProp.Name == sProp.Name && tProp.GetType() == sProp.GetType() && tProp.CanWrite);
                if (isMatched)
                {
                    var value = sProp.GetValue(source);
                    System.Reflection.PropertyInfo propertyInfo = target.GetType().GetProperty(sProp.Name);
                    propertyInfo.SetValue(target, value);
                }
            }
            return target;
        }

        public static IEnumerable<CultureInfo> GetAvailableCultures()
        {
            List<CultureInfo> result = new List<CultureInfo>(){
                //new CultureInfo("en"), //english is default, so add it manually
            };
            
            ResourceManager rm = new ResourceManager(typeof(Properties.Resources));

            CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
            foreach (CultureInfo culture in cultures)
            {
                try
                {
                    if (culture.Equals(CultureInfo.InvariantCulture)) continue; //do not use "==", won't work

                    ResourceSet rs = rm.GetResourceSet(culture, true, false);
                    if (rs != null)
                        result.Add(culture);
                }
                catch (CultureNotFoundException)
                {
                    //NOP
                }
            }
            return result;
        }

        public static void Notify(string msg,LOGLEVEL lvl= LOGLEVEL.INFO, int timeout=10000)
        {

            if (lvl != LOGLEVEL.ERROR) lvl = LOGLEVEL.INFO;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Hardcodet.Wpf.TaskbarNotification.TaskbarIcon tbIcon = (System.Windows.Application.Current as App).NotifyIcon;
                if (tbIcon.CustomBalloon != null && (tbIcon.CustomBalloon.Child is Notification))
                {
                    (tbIcon.CustomBalloon.Child as Notification).BalloonText = msg;
                    (tbIcon.CustomBalloon.Child as Notification).LogLevel = lvl;
                }
                else
                {
                    tbIcon.ShowCustomBalloon(
                    new Notification()
                        {
                            BalloonText = msg,
                            LogLevel = lvl,
                        },
                    System.Windows.Controls.Primitives.PopupAnimation.Slide,
                    timeout
                    );
                }
            }));
        }

        public static string UppercaseFirst(string s)
        {
            // Check for empty string.
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // Return char and concat substring.
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }

}
