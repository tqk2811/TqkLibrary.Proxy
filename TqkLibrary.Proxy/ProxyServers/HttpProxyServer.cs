using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using System.Text.RegularExpressions;
using System.Net.Http;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class HttpProxyServer : BaseProxyServer, IHttpProxy
    {
        static readonly HttpClientHandler httpClientHandler;
        static readonly HttpClient httpClient;
        static HttpProxyServer()
        {
            httpClientHandler = new HttpClientHandler()
            {
                UseCookies = false,
                UseProxy = false,
            };
            httpClient = new HttpClient(httpClientHandler, true);
        }


        public bool AllowHttps { get; set; } = true;
        public NetworkCredential Credentials { get; }
        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, NetworkCredential credentials = null) : base(iPEndPoint, proxySource)
        {
            this.Credentials = credentials;
        }

        protected override async Task ProxyWork(Stream stream, EndPoint remoteEndPoint)
        {
            bool client_isKeepAlive = false;
            bool should_continue = false;
            do
            {
                should_continue = false;
                List<string> client_HeaderLines = await stream.ReadHeader();
#if DEBUG
                client_HeaderLines.ForEach(x => Console.WriteLine($"{remoteEndPoint} >> {x}"));
#endif
                HeaderParse client_HeaderParse = client_HeaderLines.Parse();

                //Check Proxy-Authorization
                if (Credentials != null)
                {
                    if (client_HeaderParse.ProxyAuthorization == null)
                    {
                        //must read content if post,...
                        await stream.ReadContentAsync(client_HeaderParse.ContentLength).ConfigureAwait(false);
                        await WriteResponse407(remoteEndPoint, stream).ConfigureAwait(false);
                        //should_continue = true;//ipv6 should continue
                        continue;
                    }
                    else
                    {
                        switch (client_HeaderParse.ProxyAuthorization.Scheme)
                        {
                            case "Basic":
                                {
                                    string parameter = Encoding.UTF8.GetString(Convert.FromBase64String(client_HeaderParse.ProxyAuthorization.Parameter));
                                    string[] split = parameter.Split(':');
                                    if (split.Length == 2)
                                    {
                                        if (!split[0].Equals(Credentials.UserName, StringComparison.OrdinalIgnoreCase) ||
                                            !split[1].Equals(Credentials.Password, StringComparison.OrdinalIgnoreCase))
                                        {
                                            //must read content if post,...
                                            await stream.ReadContentAsync(client_HeaderParse.ContentLength).ConfigureAwait(false);
                                            await WriteResponse407(remoteEndPoint, stream).ConfigureAwait(false);
                                            //should_continue = true;//ipv6 should continue
                                            continue;
                                        }
                                        //else work
                                    }
                                    break;
                                }

                            default:
                                //must read content if post,...
                                await stream.ReadContentAsync(client_HeaderParse.ContentLength).ConfigureAwait(false);
                                await WriteResponse(remoteEndPoint, stream, "400 Bad Request");
                                return;
                        }
                    }
                }

                client_isKeepAlive = client_HeaderParse.IsKeepAlive;

                if ("CONNECT".Equals(client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                {
                    should_continue = await HttpsTransfer(
                        remoteEndPoint,
                        stream,
                        client_HeaderParse);
                }
                else
                {
                    should_continue = await HttpTransfer(
                        remoteEndPoint,
                        stream,
                        client_HeaderLines,
                        client_HeaderParse);
                }
            }
            while ((client_isKeepAlive || should_continue));
        }


        async Task<bool> HttpsTransfer(
            EndPoint remoteEndPoint,
            Stream stream,
            HeaderParse client_HeaderParse)
        {
            using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(client_HeaderParse.Uri);
            if (sessionSource == null)
            {
                //must read content if post,...
                await stream.ReadContentAsync(client_HeaderParse.ContentLength).ConfigureAwait(false);
                await WriteResponse(remoteEndPoint, stream, "408 Request Timeout");
                return false;
            }
            else
            {
                await WriteResponse(remoteEndPoint, stream, "200 Connection established");
            }

            using var remote_stream = sessionSource.GetStream();
            await new StreamTransferHelper(stream, remote_stream)
#if DEBUG
                .DebugName(remoteEndPoint.ToString(), client_HeaderParse.Uri.ToString())
#endif
                .WaitUntilDisconnect().ConfigureAwait(false);
            return true;
        }

        async Task<bool> HttpTransfer(
            EndPoint remoteEndPoint,
            Stream stream,
            List<string> client_HeaderLines,
            HeaderParse client_HeaderParse)
        {
            //raw http header request
            using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(client_HeaderParse.Uri);
            if (sessionSource is null)
            {
                await WriteResponse(remoteEndPoint, stream, "408 Request Timeout");
                return false;
            }
            using Stream target_Stream = sessionSource.GetStream();


            //send header to target
            List<string> headerLines = new List<string>();
            headerLines.Add($"{client_HeaderParse.Method} {client_HeaderParse.Uri.AbsolutePath} HTTP/{client_HeaderParse.Version}");
            if (!client_HeaderLines.Any(x => x.StartsWith("host: ", StringComparison.OrdinalIgnoreCase)))
            {
                headerLines.Add($"Host: {client_HeaderParse.Uri.Host}");
            }
            foreach (var line in client_HeaderLines.Skip(1)
                .Where(x => !x.StartsWith("Proxy-Authorization: ", StringComparison.OrdinalIgnoreCase)))
            {
                headerLines.Add(line);
            }

            foreach (var line in headerLines)
            {
                await target_Stream.WriteLineAsync(line).ConfigureAwait(false);
#if DEBUG
                Console.WriteLine($"{client_HeaderParse.Uri.Host} << {line}");
#endif
            }
            await target_Stream.WriteLineAsync().ConfigureAwait(false);


            //Transfer content from client to target if have
            await stream.TransferAsync(target_Stream, client_HeaderParse.ContentLength).ConfigureAwait(false);
#if DEBUG
            Console.WriteLine($"[{remoteEndPoint} >> {client_HeaderParse.Uri.Host}] {client_HeaderParse.ContentLength} bytes");
#endif
            await target_Stream.FlushAsync().ConfigureAwait(false);


            //-----------------------------------------------------
            //read header from target, and send back to client
            List<string> target_response_HeaderLines = await target_Stream.ReadHeader();
            int ContentLength = target_response_HeaderLines.GetContentLength();
            foreach (var line in target_response_HeaderLines)
            {
                await stream.WriteLineAsync(line).ConfigureAwait(false);
#if DEBUG
                Console.WriteLine($"{client_HeaderParse.Uri.Host} >> {line}");
#endif
            }
            await stream.WriteLineAsync().ConfigureAwait(false);


            //Transfer content from target to client if have
            await target_Stream.TransferAsync(stream, ContentLength).ConfigureAwait(false);
#if DEBUG
            Console.WriteLine($"[{remoteEndPoint} << {client_HeaderParse.Uri.Host}] {ContentLength} bytes");
#endif
            await stream.FlushAsync().ConfigureAwait(false);

            return true;
        }

        async Task WriteResponse407(EndPoint remoteEndPoint, Stream stream)
        {
#if DEBUG
            Console.WriteLine($"{remoteEndPoint} << HTTP/1.1 407 Proxy Authentication Required");
            Console.WriteLine($"{remoteEndPoint} << Proxy-Authenticate: Basic Scheme='Data'");
#endif
            await stream.WriteLineAsync($"HTTP/1.1 407 Proxy Authentication Required").ConfigureAwait(false);
            await stream.WriteLineAsync("Proxy-Authenticate: Basic Scheme='Data'").ConfigureAwait(false);
            await stream.WriteLineAsync().ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
        }

        async Task WriteResponse(EndPoint remoteEndPoint, Stream stream, string code_and_message, string content_message = null)
        {
            int contentLength = 0;
            byte[] content = null;
            if (!string.IsNullOrWhiteSpace(content_message))
            {
                content = Encoding.UTF8.GetBytes(content_message);
                contentLength = content.Length;
            }

#if DEBUG
            Console.WriteLine($"{remoteEndPoint} << HTTP/1.1 {code_and_message}");
            Console.WriteLine($"{remoteEndPoint} << Content-Length: {contentLength}");
#endif
            await stream.WriteLineAsync($"HTTP/1.1 {code_and_message}").ConfigureAwait(false);
            await stream.WriteLineAsync($"Content-Length: {contentLength}").ConfigureAwait(false);
            if (contentLength > 0 && content != null)
            {
                await stream.WriteLineAsync($"Content-Type: text/html; charset=utf-8").ConfigureAwait(false);
            }
            await stream.WriteLineAsync().ConfigureAwait(false);
            if (contentLength > 0 && content != null)
            {
                await stream.WriteAsync(content, 0, contentLength).ConfigureAwait(false);
            }
            await stream.FlushAsync().ConfigureAwait(false);
        }
    }
}
