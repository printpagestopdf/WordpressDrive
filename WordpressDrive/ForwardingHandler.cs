using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WordpressDrive
{
    public class ForwardingHandler : DelegatingHandler
    {
        readonly int MAXRETRIES = Settings.Instance.SysSettings.RequestRetries;
        private DateTime _lastMsgTime = DateTime.Now;
        private readonly double _msgWaitms = (double)Settings.Instance.SysSettings.RequestTimeout * 3;
        const int MAXWAIT = 5000;
        public Uri baseAddress;
        public HttpMessageHandler messageHandler;
        public CookieCollection parsedCookies;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    // base.SendAsync calls the inner handler
                    request.Headers.Remove("Cookie");
                    if (parsedCookies.Count > 0)
                    {
                        request.Headers.Add("Cookie", Utils.GetCookieHeader(parsedCookies));
                    }
                    else
                    {
                        request.Headers.Add("Cookie", "wordpress_test_cookie=WP+Cookie+check");
                    }

                    var response = await base.SendAsync(request, cancellationToken);


                    if (response.StatusCode == (HttpStatusCode)429 || response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        // 429 Too many requests
                        if (++retries > MAXRETRIES) break;
                        int wait=RetryFromHeader(response.Headers);
                        if (wait > MAXWAIT) break;
                        await Task.Delay(wait, cancellationToken);
                        continue;
                    }

                    ProcessCookieHeader(response.Headers);

                    int statusCode = (int)response.StatusCode;
                    // manual redirection to ensure cookies used although for redirect
                    if (statusCode >= 300 && statusCode <= 399)
                    {

                        var redirectUri = response.Headers.Location;
                        if (!redirectUri.IsAbsoluteUri)
                        {
                            redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);
                        }
                        request.RequestUri = redirectUri;
                        continue;
                    }

                    return response;

                }
                catch(HttpRequestException ex) when( IsSSLAuthError(ex))
                {
                    Utils.Notify(Properties.Resources.err_msg_ssl, Utils.LOGLEVEL.ERROR);
                    throw;
                }
                catch (TaskCanceledException)
                {

                    //prevent message flooding
                    if (DateTime.Now > (_lastMsgTime + TimeSpan.FromMilliseconds(_msgWaitms)))
                    {
                        _lastMsgTime = DateTime.Now;
                        Utils.Notify(Properties.Resources.err_msg_network, Utils.LOGLEVEL.ERROR);
                    }

                    throw;
                }
 
            }

            Utils.Notify(Properties.Resources.err_unknow_server_error, Utils.LOGLEVEL.ERROR);
            throw new GenericServerError();
        }

        private int RetryFromHeader(HttpResponseHeaders hdr)
        {
            if(hdr.TryGetValues("Retry-After", out IEnumerable<string> values) && values.Count<string>() > 0)
            {
                if(int.TryParse(values.First<string>(),out int seconds))
                {
                    return seconds * 1000;
                }
                try
                {
                    DateTime retry= Convert.ToDateTime(values.First<string>());
                    return (int)(retry - DateTime.Now).TotalMilliseconds;
                }
                catch { }
            }

            return MAXWAIT;
        }

        protected void ProcessCookieHeader(System.Net.Http.Headers.HttpResponseHeaders headers)
        {
            if (headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                string hdrSetCookies = "";
                foreach (string setCookie in setCookies)
                {
                    hdrSetCookies += setCookie + ",";
                }
                hdrSetCookies = hdrSetCookies.TrimEnd(new Char[] { ',' });

                CookieCollection cookies = Utils.GetAllCookiesFromHeader(hdrSetCookies, baseAddress.Host);
                parsedCookies.Add(cookies);
            }
        }

        private static bool IsSSLAuthError(Exception ex)
        {
            // Check if it's a network error
            if (ex is System.Security.Authentication.AuthenticationException)
                return true;
            if (ex.InnerException != null)
                return IsSSLAuthError(ex.InnerException);
            return false;
        }
    }

}
