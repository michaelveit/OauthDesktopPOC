using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace EcwidPoc
{
    public class OauthClient
    {
        private static readonly string BaseUrl = "https://my.ecwid.com/api/oauth";

        public static AuthInfo GetAuthInfo(string clientId, string clientSecret, string code)
        {
            CheckArgumentNull("clientId", clientId);
            CheckArgumentNull("clientSecret", clientSecret);
            CheckArgumentNull("code", code);

            // make POST request to obtain the token
            using (var client = new HttpClient())
            {
                var content = string.Format("client_id={0}&client_secrect={1}&code={2}", clientId, clientSecret, code);
                var message = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/token");
                message.Content = new StringContent(content);
                var response = client.SendAsync(message).Result;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    return null;
                }
                
                // parse the returned value
                var values = response.Content.ReadAsStringAsync().Result.Split('&')
                    .Select(x => x.Split(new[] { '=' }, count: 2))
                    .ToArray();

                // if format is: error=unauthorized_client
                if (values.Length == 1)
                {
                    var errorMessage = values[0][1];
                    throw new ApplicationException(string.Format("Error: {0}", values[0][1]));
                }

                // if format is: access_token=:accesstoken&token_type=:tokentype
                if (values.Length == 2 && values.All(x => x.Length == 2))
                {
                    var authInfoElements = values.ToDictionary(x => x[0], x => x[1]);

                    return new AuthInfo(authInfoElements["access_token"], authInfoElements["token_type"]);
                }

                return null;
            }
        }



        public static AuthInfo AskForAuthorization(string clientId, string clientSecret, TimeSpan timeout)
        {
            CheckArgumentNull("clientId", clientId);
            CheckArgumentNull("clientSecret", clientSecret);

            string code = GetCodeFromLocalHost(clientId, timeout);
            return GetAuthInfo(clientId, clientSecret, code);
        }

        private static string GetAuthorizationUrl(string clientId, string redirectUrl)
        {
            return string.Format("{0}/authorize?client_id={1}&redirect_uri={2}&response_type=code&scope=read_store_profile+read_catalog+update_catalog+read_orders", BaseUrl, clientId, redirectUrl);
        }

        private static string GetCodeFromLocalHost(string clientId, TimeSpan timeout)
        {
            const string httpTemporaryListenAddresses = "http://+:80/Temporary_Listen_Addresses/";
            const string redirectUrl = "http://localhost:80/Temporary_Listen_Addresses";

            string code;
            using (var listener = new HttpListener())
            {
                string localHostUrl = string.Format(httpTemporaryListenAddresses);

                listener.Prefixes.Add(localHostUrl);
                listener.Start();

                using (Process.Start(GetAuthorizationUrl(clientId, redirectUrl)))
                {
                    while (true)
                    {
                        var start = DateTime.Now;
                        var context = listener.GetContext(timeout);
                        var usedTime = DateTime.Now.Subtract(start);
                        timeout = timeout.Subtract(usedTime);

                        if (context.Request.Url.AbsolutePath == "/Temporary_Listen_Addresses/")
                        {
                            code = context.Request.QueryString["code"];
                            if (code == null)
                            {
                                throw new Exception("Access denied, no return code was returned");
                            }

                            var writer = new StreamWriter(context.Response.OutputStream);
                            writer.WriteLine(CloseWindowResponse);
                            writer.Flush();

                            context.Response.Close();
                            break;
                        }

                        context.Response.StatusCode = 404;
                        context.Response.Close();
                    }
                }
            }
            return code;
        }


        private static void CheckArgumentNull(string argumentName, object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(argumentName);
            }
        }

        private const string CloseWindowResponse = "<!DOCTYPE html><html><head></head><body onload=\"closeThis();\"><h1>Authorization Successful</h1><p>You can now close this window</p><script type=\"text/javascript\">function closeMe() { window.close(); } function closeThis() { window.close(); }</script></body></html>";
    }
}

