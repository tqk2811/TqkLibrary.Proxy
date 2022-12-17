using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

/* Unmerged change from project 'TqkLibrary.Proxy (net6.0)'
Before:
using TqkLibrary.Proxy.StreamHeplers;
After:
using TqkLibrary;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.StreamHeplers;
*/
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy
{
    internal class HeaderParse
    {
        public Uri Uri { get; set; }
        public string Method { get; set; }
        public string Version { get; set; }
        public bool IsKeepAlive { get; set; } = false;
        public string Host { get; set; }
        public int ContentLength { get; set; } = 0;

        public AuthenticationHeaderValue ProxyAuthorization { get; set; }
    }

    internal static class HttpUtilities
    {
        static readonly Regex regex_httpRequestMethod = new Regex("([A-z]+) ([A-z]+:\\/\\/|)(.*?) HTTP\\/([0-9\\.]{3})$");

        internal static HeaderParse Parse(this IEnumerable<string> lines)
        {
            HeaderParse headerParse = new HeaderParse();
            foreach (var line in lines)
            {
                //Proxy just care
                //+ first line
                //+ Proxy-Authorization
                //+ ContentLength
                if (string.IsNullOrWhiteSpace(headerParse.Method))
                {
                    //first line
                    Match match = regex_httpRequestMethod.Match(line);
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
                        if (Uri.TryCreate($"{scheme}://{match.Groups[3].Value}", UriKind.RelativeOrAbsolute, out Uri _uri))
                        {
                            headerParse.Uri = _uri;
                            headerParse.Method = match.Groups[1].Value;
                            headerParse.Version = match.Groups[4].Value;
                        }
                        else throw new InvalidOperationException();
                    }
                    else throw new InvalidOperationException();
                }
                else
                {
                    if (line.StartsWith("connection: ", StringComparison.OrdinalIgnoreCase))
                    {
                        headerParse.IsKeepAlive = line.Contains("keep-alive", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (string.IsNullOrWhiteSpace(headerParse.Host) && line.StartsWith(host, StringComparison.OrdinalIgnoreCase))
                    {
                        headerParse.Host = line.Substring(host.Length);
                    }
                    else if (headerParse.ProxyAuthorization is null && line.StartsWith(proxy_authorization, StringComparison.OrdinalIgnoreCase))
                    {
                        //https://www.iana.org/assignments/http-authschemes/http-authschemes.xhtml
                        string value = line.Substring(proxy_authorization.Length).Trim(' ');
                        int index = value.IndexOf(' ');
                        if (index > 0)
                        {
                            headerParse.ProxyAuthorization = new AuthenticationHeaderValue(value.Substring(0, index), value.Substring(index + 1));
                        }
                    }
                    else if (line.StartsWith(content_length, StringComparison.OrdinalIgnoreCase))
                    {
                        headerParse.ContentLength = int.Parse(line.Substring(content_length.Length).Trim());
                    }
                }
            }
            return headerParse;
        }
        const string proxy_authorization = "Proxy-Authorization: ";
        const string content_length = "content-length: ";
        const string host = "Host: ";
        internal static int GetContentLength(this IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith(content_length, StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Substring(content_length.Length).Trim(), out int result))
                    {
                        return result;
                    }
                }
            }
            return 0;
        }

        internal static async Task<List<string>> ReadHeader(this Stream stream, CancellationToken cancellationToken = default)
        {
            List<string> lines = new List<string>();
            while (true)
            {
                //if (streamReader.EndOfStream)
                //    break;

                string line = await stream.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }
                else
                {
                    lines.Add(line);
                    if (lines.Sum(x => x.Length) > Singleton.HeaderMaxLength)
                        throw new InvalidDataException("Header too long");
                }
            }
            return lines;
        }
    }
}
