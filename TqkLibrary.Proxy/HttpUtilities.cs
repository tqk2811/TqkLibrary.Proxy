using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
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
    internal static class HttpUtilities
    {
        internal static int GetContentLength(this IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("content-length:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Substring("content-length:".Length).Trim(), out int result))
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

                string line = await stream.ReadLineAsync(cancellationToken);

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
