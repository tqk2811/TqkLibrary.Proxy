using System.Net.Http.Headers;
using System.Text.RegularExpressions;

/* Unmerged change from project 'TqkLibrary.Proxy (net6.0)'
Before:
using TqkLibrary.Proxy.StreamHeplers;
After:
using TqkLibrary;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.StreamHeplers;
*/

namespace TqkLibrary.Proxy.Helpers
{
    internal class HeaderRequestParse
    {
        static readonly Regex _regex_httpRequestMethod = new Regex("^([A-z]+) ([A-z]+:\\/\\/|)(.*?) HTTP\\/([0-9\\.]{3})$");
        private HeaderRequestParse(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentNullException(nameof(line));

            Match match = _regex_httpRequestMethod.Match(line);
            if (match.Success)
            {
                //Uri uri0 = new Uri("http://127.0.0.1:13566");
                //Uri uri1 = new Uri("http://[::1]:13566");
                //Uri uri2 = new Uri("httpbin.org:80");//must ->http://httpbin.org:80
                //Uri uri3 = new Uri("http://httpbin.org");
                //Uri uri4 = new Uri("http://httpbin.org:8080");
                //Uri uri5 = new Uri("wss://httpbin.org:8080/abc");
                string scheme = match.Groups[2].Value.TrimEnd(':', '/');
                if (string.IsNullOrWhiteSpace(scheme))
                {
                    if (match.Groups[3].Value.EndsWith(":443")) scheme = "https";
                    else scheme = "http";
                }
                if (Uri.TryCreate($"{scheme}://{match.Groups[3].Value}", UriKind.RelativeOrAbsolute, out Uri? _uri))
                {
                    Uri = _uri;
                    Method = match.Groups[1].Value;
                    Version = match.Groups[4].Value;
                }
                else throw new InvalidDataException($"Invalid header method: '{line}'");
            }
            else throw new InvalidDataException($"Invalid header method: '{line}'");

        }
        public Uri Uri { get; private set; }
        public string Method { get; private set; }
        public string Version { get; private set; }

        public bool IsKeepAlive { get; private set; } = false;
        public int ContentLength { get; private set; } = 0;
        public string? Host { get; private set; }
        public AuthenticationHeaderValue? ProxyAuthorization { get; set; }


        internal static HeaderRequestParse ParseRequest(IEnumerable<string> lines)
        {
            HeaderRequestParse headerRequestParse = new HeaderRequestParse(lines.FirstOrDefault());

            var dict = lines.Skip(1)
                .Select(x => x.Split(':'))
                .Where(x => x.Length == 2)
                .ToDictionary(k => k[0].Trim().ToLower(), v => v[1].Trim());

            if (dict.TryGetValue("proxy-connection", out string? proxy_connection))
            {
                headerRequestParse.IsKeepAlive = "keep-alive".Equals(proxy_connection, StringComparison.OrdinalIgnoreCase);
            }
            if (dict.TryGetValue("host", out string? host))
            {
                headerRequestParse.Host = host;
            }
            if (dict.TryGetValue("proxy-authorization", out string? Proxy_Authorization))
            {
                //https://www.iana.org/assignments/http-authschemes/http-authschemes.xhtml
                string[] split = Proxy_Authorization.Split(' ');
                if (split.Length == 2)
                {
                    headerRequestParse.ProxyAuthorization = new AuthenticationHeaderValue(split[0], split[1]);
                }
            }
            if (dict.TryGetValue("content-length", out string? content_length) &&
                int.TryParse(content_length, out int int_cl))
            {
                headerRequestParse.ContentLength = int_cl;
            }

            return headerRequestParse;
        }
    }
}
