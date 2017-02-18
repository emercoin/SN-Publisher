namespace EmercoinDPOSNP
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Text;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class JsonRpcClient
    {
        private string url;
        private string username;
        private string password;

        static JsonRpcClient()
        {
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 5;
            WebRequest.DefaultWebProxy = WebRequest.GetSystemWebProxy();
        }

        private JsonRpcClient(string url, string username, string password)
        {
            this.url = url;
            this.username = username;
            this.password = password;
        }

        public static JsonRpcClient Create(string host, string port, string username, string password)
        {
            string url = "http://" + host + ":" + port;
            return new JsonRpcClient(url, username, password);
        }

        public JObject SendCommand(JObject command)
        {
            command["id"] = "myRequestId";
            string request = command.ToString(Formatting.None);
            try {
                string resultStr = this.post(request);
                return JObject.Parse(resultStr);
            }
            catch (WebException ex) {
                throw new JsonRpcException(request, ex);
            }
        }

        private string post(string postData)
        {
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(this.url);
            request.KeepAlive = false;
            request.AllowAutoRedirect = true;
            request.Method = "POST";
            request.UserAgent = "Emercoin DPO SN Publisher";

            request.PreAuthenticate = true;
            try {
                string encoded = System.Convert.ToBase64String(System.Text.Encoding.GetEncoding("ISO-8859-1").GetBytes(this.username + ":" + this.password));
                request.Headers.Add("Authorization", "Basic " + encoded);

                string proxyUrl = System.Configuration.ConfigurationManager.AppSettings["DebugProxy"];
                if (proxyUrl != null) {
                    request.Proxy = new WebProxy(proxyUrl);
                    request.Proxy.Credentials = CredentialCache.DefaultNetworkCredentials;
                }

                var encoding = new System.Text.UTF8Encoding();
                byte[] requestBuffer = encoding.GetBytes(postData);

                request.ContentLength = requestBuffer.Length;
                request.ContentType = @"application/json";

                using (Stream requestStream = request.GetRequestStream()) {
                    requestStream.Write(requestBuffer, 0, requestBuffer.Length);
                }

                using (WebResponse response = request.GetResponse()) {
                    byte[] responseBuffer = new byte[response.ContentLength];
                    using (Stream responseStream = response.GetResponseStream()) {
                        responseStream.Read(responseBuffer, 0, (int)response.ContentLength);
                        responseStream.Close();
                    }

                    return encoding.GetString(responseBuffer);
                }
            }
            catch {
                request.Abort();
                throw;
            }
        }
    }

    internal class JsonRpcException : Exception
    {
        public JsonRpcException(string request, WebException ex)
            : base("JSON RPC command failed", ex)
        {
            this.Request = request;
            if (ex.Response != null) {
                this.Response = new StreamReader(ex.Response.GetResponseStream()).ReadToEnd();
            }
        }

        public string Request { get; private set; }
        public string Response { get; private set; }
    }
}
