using System.Collections.Specialized;
using System.Net;
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
    public class HeaderResponseParse
    {
        static readonly Regex regex_httpResponseStatus = new Regex("^HTTP\\/([0-9\\.]{3}) (\\d{3}) (.*?)$");
        private HeaderResponseParse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                throw new ArgumentNullException(nameof(line));

            Match match = regex_httpResponseStatus.Match(line);
            if (match.Success)
            {
                Version = match.Groups[1].Value;
                StatusCode = int.Parse(match.Groups[2].Value);
                StatusMessage = match.Groups[3].Value;
            }
            else throw new InvalidDataException($"Invalid header method response: '{line}'");
        }
        public string Version { get; set; }
        public int StatusCode { get; set; }
        public string StatusMessage { get; set; }

        public HttpStatusCode HttpStatusCode { get { return (HttpStatusCode)StatusCode; } }
        public bool IsKeepAlive { get; set; } = false;
        public int ContentLength { get; set; } = 0;


        public static HeaderResponseParse ParseResponse(IEnumerable<string> lines)
        {
            HeaderResponseParse responseStatusCode = new HeaderResponseParse(lines.FirstOrDefault()!);
            NameValueCollection nameValueCollection = new NameValueCollection();
            foreach (var line in lines
                .Skip(1)
                .Select(x => x.Split(':'))
                .Where(x => x.Length == 2))
            {
                nameValueCollection.Add(line[0].ToLower(), line[1]);
            }

            if (nameValueCollection.TryGetValues("proxy-connection", out string[]? proxy_connection))
            {
                responseStatusCode.IsKeepAlive = proxy_connection!.Any(x => x.Contains("keep-alive", StringComparison.OrdinalIgnoreCase));
            }
            if (nameValueCollection.TryGetValues("content-length", out string[]? content_length) &&
                int.TryParse(content_length!.First(), out int int_cl))
            {
                responseStatusCode.ContentLength = int_cl;
            }

            return responseStatusCode;
        }
    }
}
