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

namespace TqkLibrary.Proxy.ProxyServers
{
    internal class HeaderParse
    {
        public Uri Uri { get; set; }
        public string Method { get; set; }
        public string Version { get; set; }
        public bool IsKeepAlive { get; set; } = false;
        public int ContentLength { get; set; }

        public AuthenticationHeaderValue ProxyAuthorization { get; set; }
    }

    internal static class HttpUtilities
    {
        static readonly Regex regex_httpMethod = new Regex("([A-z]+) (.*?) HTTP/[0-9.]{3}");

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
                    Match match = regex_httpMethod.Match(line);
                    if (match.Success)
                    {
                        if (Uri.TryCreate(match.Groups[2].Value, UriKind.RelativeOrAbsolute, out Uri _uri))
                        {
                            headerParse.Uri = _uri;
                            headerParse.Method = match.Groups[1].Value;
                            headerParse.Version = match.Groups[2].Value;
                        }
                        else throw new InvalidOperationException();
                    }
                    else throw new InvalidOperationException();
                }
                else if (headerParse.ProxyAuthorization == null && AuthenticationHeaderValue.TryParse(line, out var _authenticationHeaderValue))
                {
                    //+ Proxy-Authorization
                    if ("Proxy-Authorization".Equals(_authenticationHeaderValue.Scheme, StringComparison.OrdinalIgnoreCase))
                    {
                        headerParse.ProxyAuthorization = _authenticationHeaderValue;
                    }
                }
                else
                {
                    if (line.StartsWith("connection: ", StringComparison.OrdinalIgnoreCase))
                    {
                        headerParse.IsKeepAlive = line.Contains("keep-alive", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (line.StartsWith("content-length: ", StringComparison.OrdinalIgnoreCase))
                    {
                        headerParse.ContentLength = int.Parse(line.Substring(16).Trim());
                    }
                }
            }
            return headerParse;
        }

        internal static async Task<List<string>> ReadHeader(this StreamReader streamReader)
        {
            List<string> lines = new List<string>();
            while (true)
            {
                if (streamReader.EndOfStream)
                    break;

                string line = await streamReader.ReadLineAsync().ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }
                else
                {
                    lines.Add(line);
                }
            }
            return lines;
        }
    }
}
