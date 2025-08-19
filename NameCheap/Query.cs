using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace NameCheap
{
    internal class Query
    {
        private readonly XNamespace _ns = XNamespace.Get("http://api.namecheap.com/xml.response");
        private readonly GlobalParameters _globals;
        private List<KeyValuePair<string, string>> _parameters = new List<KeyValuePair<string, string>>();

        internal Query(GlobalParameters globals)
        {
            if (globals == null)
                throw new ArgumentNullException("globals");

            _globals = globals;
        }

        internal Query AddParameter(string key, string value)
        {
            _parameters.Add(new KeyValuePair<string, string>(key, value));
            return this;
        }

        private string BuildUrl(string command)
        {
            StringBuilder url = new StringBuilder();
            url.Append(_globals.IsSandBox ? "https://api.sandbox.namecheap.com/xml.response?" : "https://api.namecheap.com/xml.response?");
            url.Append("Command=").Append(Uri.EscapeDataString(command))
               .Append("&ApiUser=").Append(Uri.EscapeDataString(_globals.ApiUser))
               .Append("&UserName=").Append(Uri.EscapeDataString(_globals.UserName))
               .Append("&ApiKey=").Append(Uri.EscapeDataString(_globals.ApiKey))
               .Append("&ClientIp=").Append(Uri.EscapeDataString(_globals.CLientIp));

            foreach (KeyValuePair<string, string> param in _parameters)
            {
                url.Append("&")
                   .Append(Uri.EscapeDataString(param.Key))
                   .Append("=")
                   .Append(Uri.EscapeDataString(param.Value ?? string.Empty));
            }

            return url.ToString();
        }

        internal XDocument Execute(string command)
        {
            string url = BuildUrl(command);
            XDocument doc = XDocument.Parse(new WebClient().DownloadString(url));

            if (doc.Root.Attribute("Status").Value.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                throw new ApplicationException(string.Join(",", doc.Root.Element(_ns + "Errors").Elements(_ns + "Error").Select(o => o.Value).ToArray()));
            else
                return doc;
        }

        internal async Task<XDocument> ExecuteAsync(string command, CancellationToken cancellationToken = default)
        {
            string url = BuildUrl(command);
            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                XDocument doc = XDocument.Parse(xml);

                if (doc.Root.Attribute("Status").Value.Equals("ERROR", StringComparison.OrdinalIgnoreCase))
                    throw new ApplicationException(string.Join(",", doc.Root.Element(_ns + "Errors").Elements(_ns + "Error").Select(o => o.Value).ToArray()));
                else
                    return doc;
            }
        }
    }
}
