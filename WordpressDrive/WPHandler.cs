using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.IO;


namespace WordpressDrive
{
    class WPHandler:IDisposable
    {
        protected class HttpResponse:IDisposable
        {
            public HttpResponse()
            {
                content = "";
                binaryContent = new byte[] { };
                streamContent = new MemoryStream();
                hdr = (new HttpResponseMessage()).Headers;
            }

            public HttpResponse(string content, HttpResponseHeaders hdr)
            {
                this.content = content;
                this.hdr = hdr;
            }
            public HttpResponse(byte[] content, HttpResponseHeaders hdr)
            {
                this.binaryContent = content;
                this.hdr = hdr;
            }
            public HttpResponse(Stream content, HttpResponseHeaders hdr)
            {
                this.streamContent = content;
                this.hdr = hdr;
            }
            public string content;
            public byte[] binaryContent;
            public HttpResponseHeaders hdr;
            public Stream streamContent;

            public void Dispose()
            {
                if(streamContent != null)
                    ((IDisposable)streamContent).Dispose();
            }
        }

        private HttpClient client;
        Uri baseAddress;
        readonly Uri apiAddress;
        public static string defApiPrefix = "wp/v2/";
        HttpClientHandler handler = new HttpClientHandler();
        CookieCollection parsedCookies = new CookieCollection();
        private string _nonce = null;

        private readonly Settings.HostSettings _curHostSettings;

        public WPHandler(Settings.HostSettings curHostSettings)
        {
            this._curHostSettings = curHostSettings;

            if (Settings.Instance.SysSettings.AcceptAllSSLCerts)
            {
                //Accept non verifyable certificates - disable for real environment
                System.Net.ServicePointManager.ServerCertificateValidationCallback +=
                               (sender, cert, chain, sslPolicyErrors) => true;
            }

            this.baseAddress = new Uri(curHostSettings.HostUrl);

            handler.AllowAutoRedirect = false;
            handler.UseCookies = false;
            ForwardingHandler forwardingHandler = new ForwardingHandler()
            {
                InnerHandler = handler,
                baseAddress = baseAddress,
                parsedCookies = parsedCookies,
            };

            client = new HttpClient(forwardingHandler) {
                Timeout = TimeSpan.FromMilliseconds(Settings.Instance.SysSettings.RequestTimeout),
            };

            client.DefaultRequestHeaders.Referrer = this.baseAddress;
            client.DefaultRequestHeaders.Host = this.baseAddress.Host;
            client.DefaultRequestHeaders.Add("Origin", this.baseAddress.AbsoluteUri);
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Settings.Instance.SysSettings.UserAgent);
            //client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0, Win64, x64, rv:81.0) Gecko/20100101 Firefox/81.0");

            this.apiAddress = GetApiUrl(curHostSettings.HostUrl);

        }

        private Uri GetApiUrl(string hostUrl)
        {
            //Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPRequest(hostUrl, HttpMethod.Head));
            Task<HttpResponse> task = WPRequest(hostUrl, HttpMethod.Head);
            task.Wait();

            if (!task.Result.hdr.TryGetValues("Link", out IEnumerable<string> links)) throw new AppException<ApiUrlException>("No 'Link' header");

            string apiUrl = null;
            foreach (string link in links)
            {
                if(link.Contains("https://api.w.org"))
                {
                    apiUrl = link.Split('>')[0];
                    apiUrl = apiUrl.TrimStart('<');
                    if (!apiUrl.EndsWith("/")) apiUrl = apiUrl + "/";
                    break;
                }
            }

            if(apiUrl == null ) throw new AppException<ApiUrlException>("No Api header");

            return new Uri(apiUrl);
        }

        protected bool AuthenticationDlg( string hint)
        {
            bool retVal=false;
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Authentication auth = new Authentication();
                auth.statusText.Content = hint;
                auth.DataContext = this._curHostSettings;
                if((bool)auth.ShowDialog() == true)   retVal = true;
            }));

            return retVal;
        }

        public void Authenticate()
        {
            if (this._curHostSettings.AnonymousLogin) return;

            if(string.IsNullOrWhiteSpace(this._curHostSettings.Username) || string.IsNullOrWhiteSpace(this._curHostSettings.Password))
            {
                bool  auth=AuthenticationDlg(Properties.Resources.Supply_auth_data);
                if (!auth)  if (!auth) throw new AppException<AuthCancelledException>(Properties.Resources.Login_cancelled);
                if (this._curHostSettings.AnonymousLogin) return; //anonymous login
            }

            while (true)
            {
                try
                {
                    parsedCookies = new CookieCollection();
                    var values = new Dictionary<string, string>
                    {
                        { "log", this._curHostSettings.Username },
                        { "pwd", this._curHostSettings.PasswordDec },
                        { "wp-submit", "Anmelden" },
                        { "redirect_to", this.baseAddress.AbsoluteUri + "wp-admin/" },
                        { "testcookie", "1"}
                    };
                    
                    Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPRequest(this.baseAddress.AbsoluteUri + "wp-login.php", HttpMethod.Post, null, values,false,true));
                    string responseString = task.Result.content;

                    string pattern = @"^var\s*wpApiSettings.*nonce[\""']\s*:\s*[\""']([^\""']*).*$";
                    Match m = Regex.Match(responseString, pattern, RegexOptions.Multiline);
                    if (!m.Success || m.Groups.Count < 2)
                    {
                        GetNonce();
                    }
                    else
                        this._nonce = m.Groups[1].Value;

                    break;
                }
                catch(AppException<RequestException>)
                {
                    bool auth = AuthenticationDlg(Properties.Resources.Login_failed_retry);
                    if (!auth) throw new AppException<AuthCancelledException>(Properties.Resources.Login_cancelled);
                    if (this._curHostSettings.AnonymousLogin) return; //anonymous login
                }
            }

            if (!this._curHostSettings.AnonymousLogin)
            {
                this._curHostSettings.CanModifyAttachment = CheckModifyAttachment();
            }

        }

        protected bool CheckModifyAttachment()
        {
            bool ret = false;

            try
            {
                string raw = WPAPIGet("");
                if (string.IsNullOrEmpty(raw)) throw new Exception();

                var data = Utils.CleanJson<JObject>(raw);
                JObject routes = (JObject)data.SelectToken("routes", errorWhenNoMatch: true);
                ret = routes.ContainsKey(@"/wp-drive/v1/attachment/(?P<id>[\d]+)");
            }
            catch { }

            return ret;
        }

        protected string GetNonce()
        {
            if (this._curHostSettings.AnonymousLogin) return null;

            if (this._nonce != null) return _nonce;

            Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPRequest(this.baseAddress.AbsoluteUri + "wp-admin/post-new.php", HttpMethod.Get,null,null,false,true));
            string responseString = task.Result.content;

            string pattern = @"^var\s*wpApiSettings.*nonce[\""']\s*:\s*[\""']([^\""']*).*$";
            Match m = Regex.Match(responseString, pattern, RegexOptions.Multiline);
            if (!m.Success || m.Groups.Count < 2)
            {
                throw new AppException<RequestException>("Error retrieving Nonce");
            }
            this._nonce = m.Groups[1].Value;
            return this._nonce;
        }

        public Stream WPAPIReadContent(WPObject wpObject)
        {
            Stream ret = null;

            try
            {
                if (string.IsNullOrWhiteSpace(wpObject.ApiContent)) return null;
                if (wpObject.Type == "attachment")
                {
                    return WPAPIDownload(wpObject.ApiContent, out HttpResponseHeaders hdrs);
                }
                string response = WPAPIGet(wpObject.ApiContent);

                JObject oResponse = JObject.Parse(response);

                JToken token;
                if ((token = oResponse.SelectToken("content.raw", errorWhenNoMatch: false)) != null)
                    ret = Utils.StreamFromString(token.ToString());
                else if ((token = oResponse.SelectToken("content.rendered", errorWhenNoMatch: false)) != null)
                    ret = Utils.StreamFromString(token.ToString());
                else
                    ret = Utils.StreamFromString("");
            }
            catch
            {
                //Utils.Notify("Unable to retrieve content", Utils.LOGLEVEL.ERROR);
                //ret = Utils.StreamFromString("");
                throw;
            }
            return ret;
 
        }

        public Dictionary<string,WPObject> WPAPIGetTypes()
        {
            string uriEndpoint = $"{defApiPrefix}types";
            Dictionary<string, WPObject> objList = new Dictionary<string, WPObject>();
            string raw = WPAPIGet(uriEndpoint);
            if (string.IsNullOrEmpty(raw)) return objList;

            var wpObjects = Utils.CleanJson<JObject>(raw);

            foreach (JToken wpData in wpObjects.PropertyValues())
            {
                WPObject wpObject = new WPObject(default(WPObject)) { role = "_MainParent", isDirectory = true };
                JToken token;
                if ((token = wpData.SelectToken("name", errorWhenNoMatch: false)) != null)
                    wpObject.Title = token.ToString();

                if ((token = wpData.SelectToken("slug", errorWhenNoMatch: false)) != null)
                    wpObject.Type = token.ToString();

                if ((token = wpData.SelectToken("rest_base", errorWhenNoMatch: false)) != null)
                    objList.Add(token.ToString(),wpObject);
            }

            return objList;
        }

        public List<WPObject> WPAPIGetObjects(string uriEndpoint, Dictionary<string, string> args = null)
        {
            return WPAPIGetObjects( uriEndpoint, out  int totalItems, args);
        }


        public List<WPObject> WPAPIGetObjects(string uriEndpoint, out int totalItems,Dictionary<string, string> args = null)
        {
            List<WPObject> objList = new List<WPObject>();
            string raw = WPAPIGet(uriEndpoint, out HttpResponseHeaders hdrs, args);
            if (string.IsNullOrEmpty(raw)) { totalItems = 0; return objList; }

            var wpObjects = Utils.CleanJson<JArray>(raw);

            foreach (dynamic wpObj in wpObjects)
            {
                objList.Add(new WPObject(wpObj));
            }
            if (hdrs.Contains("X-WP-Total"))
                int.TryParse(hdrs.GetValues("X-WP-Total").First<string>(), out totalItems);
            else
                totalItems = -1;

            return objList;
        }

        public int WPAPIGetTotalItems(string uriEndpoint, Dictionary<string, string> args = null)
        {

            int totalItems = -1;
            string response = WPAPIGet(uriEndpoint + $"&per_page=1", out HttpResponseHeaders hdrs, args);

            if (hdrs.Contains("X-WP-Total"))
                int.TryParse(hdrs.GetValues("X-WP-Total").First<string>(), out totalItems);

            return totalItems;
        }

        public string WPAPIGet(string uriEndpoint, out HttpResponseHeaders hdrs, Dictionary<string, string> args = null)
        {
            string nonce = GetNonce();
            //Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPRequest(this.apiAddress.AbsoluteUri + uriEndpoint, HttpMethod.Get, nonce));
            Task<HttpResponse> task = WPRequest(this.apiAddress.AbsoluteUri + uriEndpoint, HttpMethod.Get, nonce);
            task.Wait();
            string responseString = task.Result.content;
            hdrs = task.Result.hdr;
            return responseString;
        }

        public Stream WPAPIDownload(string uriEndpoint, out HttpResponseHeaders hdrs, Dictionary<string, string> args = null)
        {
            string nonce = GetNonce();
            Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPRequest(uriEndpoint, HttpMethod.Get, nonce,null,true));
            hdrs = task.Result.hdr;
            return task.Result.streamContent;;

        }

        public string WPAPIGet(string uriEndpoint, Dictionary<string, string> args = null)
        {
            return WPAPIGet( uriEndpoint, out HttpResponseHeaders hdrs, args);
        }


        public string WPAPIReplaceMedia(WPObject wPObject, Stream data)
        {
            Dictionary<string, string> args = new Dictionary<string, string>()
            {
                {wPObject.id ,"id"  },
            };
            string nonce = GetNonce();
            Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPUpload(this.apiAddress.AbsoluteUri + "wp-drive/v1/attachment/" + wPObject.id, HttpMethod.Post, nonce, null, data));
            string responseString = task.Result.content;

            return responseString;
        }

        public bool WPAPIDelete(WPObject wPObject)
        {
            if (wPObject == null || string.IsNullOrEmpty(wPObject.ApiItem)) return false;

            string url = wPObject.ApiItem + "?_method=DELETE" + (wPObject.Type == "attachment" ? "&force=true" : "");
            string response = WPAPIPost( url);
            return true;
        }

        public WPObject WPAPICreateOrUpdate(WPObject wPObject, Stream streamData)
        {

            WPObject result = null;
            if (wPObject.isNew)
            {
                switch (wPObject.Type)
                {
                    case "attachment":
                        {
                            result = WPAPICreateMedia(wPObject, streamData);
                        }
                        break;
                    default:
                        {
                            Dictionary<string, string> args = new Dictionary<string, string>()
                            {
                                {"title", wPObject.Title },
                                {"content", (new StreamReader(streamData)).ReadToEnd() },
                            };
                            string response = WPAPIPost($"{wPObject.ApiListBase}", args);
                            var JwpObject = JObject.Parse(response) as JObject;
                            result = new WPObject(JwpObject);

                        }
                        break;
                }
            }
            else
            {
                result = wPObject;
                if (wPObject.Type == "attachment")
                {
                    string response = WPAPIReplaceMedia(wPObject, streamData);
                }
                else
                {
                    Dictionary<string, string> args = new Dictionary<string, string>()
                    {
                        {"id", wPObject.id },
                        {"content", (new StreamReader(streamData)).ReadToEnd() },
                    };
                    string response = WPAPIPost(wPObject.ApiItem, args);
                }
            }

            return result;
        }

        public WPObject WPAPICreateMedia(WPObject wPObject, Stream data)
        {
            if (this._curHostSettings.AnonymousLogin) return null;

            Dictionary<string, string> args = new Dictionary<string, string>()
            {
                 {"title" ,wPObject.Title  },
            };
            string nonce = GetNonce();
            Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPUpload($"{this.apiAddress.AbsoluteUri}{wPObject.ApiListBase}/", HttpMethod.Post, nonce, args, data, wPObject.filename));
            string responseString = task.Result.content;

            var JwpObject = JObject.Parse(responseString) as JObject;
            WPObject resultWpObject = new WPObject(JwpObject);

            return resultWpObject;
        }

        public string WPAPIPost(string uriEndpoint, Dictionary<string, string> args = null)
        {
            string nonce = GetNonce();
            Task<HttpResponse> task = Task.Run<HttpResponse>(async () => await WPRequest(this.apiAddress.AbsoluteUri + uriEndpoint, HttpMethod.Post, nonce, args));
            string responseString = task.Result.content;

            return responseString;
        }

        protected async Task<HttpResponse> WPRequest(string baseUri, HttpMethod method, string nonce = null, Dictionary<string, string> args = null, bool asStream = false, bool formEncoded=false)
        {
            HttpResponseMessage response=null;
            try
            {
                HttpRequestMessage msg = new HttpRequestMessage(method, baseUri);

                if (args != null && args.Count > 0)
                {
                    if (!formEncoded)
                    {
                        string json = JsonConvert.SerializeObject(args, Formatting.Indented);
                        msg.Content = new StringContent(json, Encoding.UTF8, "application/json");
                    }
                    else
                    {
                        FormUrlEncodedContent encArgs = new FormUrlEncodedContent(args);
                        msg.Content = encArgs;
                    }
                }
                if (nonce != null)
                {
                    msg.Headers.Add("X-WP-Nonce", nonce);
                }
 
                response = await client.SendAsync(msg);
                response.EnsureSuccessStatusCode();

                if (asStream)
                    return new HttpResponse(response.Content.ReadAsStreamAsync().Result, response.Headers);
                else
                    return new HttpResponse(response.Content.ReadAsStringAsync().Result, response.Headers);

            }
            catch(HttpRequestException reqEx)
            {
                throw new WPRequestException(reqEx.Message, reqEx, response);
            }
        }

        protected async Task<HttpResponse> WPUpload(string baseUri, HttpMethod method, string nonce = null, Dictionary<string, string> args = null, Stream attachment = null, string fileName = null)
        {
            HttpResponseMessage response=null;
            try
            {
                HttpRequestMessage msg = new HttpRequestMessage(method, baseUri) {
                    Content = new MultipartFormDataContent(Guid.NewGuid().ToString()),
                 };

                if (args != null && args.Count > 0)
                {
                    foreach (KeyValuePair<string, string> arg in args)
                    {
                        (msg.Content as MultipartFormDataContent).Add(new StringContent(arg.Value), arg.Key);
                    }

                }
                if (nonce != null)
                {
                    msg.Headers.Add("X-WP-Nonce", nonce);
                }

                if (attachment != null)
                {
                    StreamContent filePart = new StreamContent(attachment);

                    filePart.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                    {
                        Name = "file", // <- included line...
                        FileName = "dummy.txt",
                    };
                    if (fileName != null) filePart.Headers.ContentDisposition.FileName = fileName;

                    (msg.Content as MultipartFormDataContent).Add(filePart);
                }

                response = await client.SendAsync(msg);
                response.EnsureSuccessStatusCode();

                return new HttpResponse(response.Content.ReadAsStringAsync().Result, response.Headers);

            }
            catch (HttpRequestException reqEx)
            {
                throw new WPRequestException(reqEx.Message, reqEx, response);
            }
        }

        public void Dispose()
        {
            handler.Dispose();
            client.Dispose();
        }

    }
}
