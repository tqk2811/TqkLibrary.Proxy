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
                if (client_HeaderLines.Count == 0)
                    return;//client stream closed
#if DEBUG
                client_HeaderLines.ForEach(x => Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} >> {x}"));
#endif
                HeaderRequestParse client_HeaderParse = client_HeaderLines.ParseRequest();

                //Check Proxy-Authorization
                if (Credentials != null)
                {
                    if (client_HeaderParse.ProxyAuthorization == null)
                    {
                        //must read content if post,...
                        await stream.ReadContentAsync(client_HeaderParse.ContentLength);
                        should_continue = await WriteResponse407(remoteEndPoint, stream);
                        continue;
                    }
                    else
                    {
                        switch (client_HeaderParse.ProxyAuthorization.Scheme.ToLower())
                        {
                            case "basic":
                                {
                                    string parameter = Encoding.UTF8.GetString(Convert.FromBase64String(client_HeaderParse.ProxyAuthorization.Parameter));
                                    string[] split = parameter.Split(':');
                                    if (split.Length == 2)
                                    {
                                        if (!split[0].Equals(Credentials.UserName, StringComparison.OrdinalIgnoreCase) ||
                                            !split[1].Equals(Credentials.Password, StringComparison.OrdinalIgnoreCase))
                                        {
                                            //must read content if post,...
                                            await stream.ReadContentAsync(client_HeaderParse.ContentLength);
                                            should_continue = await WriteResponse407(remoteEndPoint, stream);
                                            continue;
                                        }
                                        //else work
                                    }
                                    break;
                                }

                            default:
                                //must read content if post,...
                                await stream.ReadContentAsync(client_HeaderParse.ContentLength);
                                should_continue = await WriteResponse(remoteEndPoint, stream, true, "400 Bad Request");
                                continue;
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
            HeaderRequestParse client_HeaderParse)
        {
            using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(client_HeaderParse.Uri);
            if (sessionSource == null)
            {
                //must read content if post,...
                await stream.ReadContentAsync(client_HeaderParse.ContentLength);
                return await WriteResponse(remoteEndPoint, stream, true, "408 Request Timeout");
            }
            else
            {
                await WriteResponse(remoteEndPoint, stream, true, "200 Connection established");
            }

            using var remote_stream = sessionSource.GetStream();
            await new StreamTransferHelper(stream, remote_stream)
#if DEBUG
                .DebugName(remoteEndPoint.ToString(), client_HeaderParse.Uri.ToString())
#endif
                .WaitUntilDisconnect();
            return true;
        }

        async Task<bool> HttpTransfer(
            EndPoint remoteEndPoint,
            Stream stream,
            List<string> client_HeaderLines,
            HeaderRequestParse client_HeaderParse)
        {
            //raw http header request
            using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(client_HeaderParse.Uri);
            if (sessionSource is null)
            {
                return await WriteResponse(remoteEndPoint, stream, true, "408 Request Timeout");
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
                .Where(x => !x.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase)))
            {
                headerLines.Add(line);
            }

            foreach (var line in headerLines)
            {
                await target_Stream.WriteLineAsync(line);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_HeaderParse.Uri.Host} << {line}");
#endif
            }
            await target_Stream.WriteLineAsync();


            //Transfer content from client to target if have
            await stream.TransferAsync(target_Stream, client_HeaderParse.ContentLength);
#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] [{remoteEndPoint} >> {client_HeaderParse.Uri.Host}] {client_HeaderParse.ContentLength} bytes");
#endif
            await target_Stream.FlushAsync();


            //-----------------------------------------------------
            //read header from target, and send back to client
            List<string> target_response_HeaderLines = await target_Stream.ReadHeader();
            int ContentLength = target_response_HeaderLines.GetContentLength();
            foreach (var line in target_response_HeaderLines)
            {
                await stream.WriteLineAsync(line);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_HeaderParse.Uri.Host} >> {line}");
#endif
            }
            await stream.WriteLineAsync();


            //Transfer content from target to client if have
            await target_Stream.TransferAsync(stream, ContentLength);
#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] [{remoteEndPoint} << {client_HeaderParse.Uri.Host}] {ContentLength} bytes");
#endif
            await stream.FlushAsync();

            return true;
        }

        async Task<bool> WriteResponse407(EndPoint remoteEndPoint, Stream stream)
        {
#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << HTTP/1.1 407 Proxy Authentication Required");
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << Proxy-Authenticate: Basic Scheme='Data'");
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << Connection: keep-alive");
#endif
            await stream.WriteLineAsync($"HTTP/1.1 407 Proxy Authentication Required");
            await stream.WriteLineAsync("Proxy-Authenticate: Basic Scheme='Data'");
            await stream.WriteLineAsync("Connection: keep-alive");
            await stream.WriteLineAsync();
            await stream.FlushAsync();
            return true;
        }

        async Task<bool> WriteResponse(EndPoint remoteEndPoint, Stream stream, bool isKeepAlive, string code_and_message, string content_message = null)
        {
            int contentLength = 0;
            byte[] content = null;
            if (!string.IsNullOrWhiteSpace(content_message))
            {
                content = Encoding.UTF8.GetBytes(content_message);
                contentLength = content.Length;
            }

#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << HTTP/1.1 {code_and_message}");
            if (isKeepAlive) Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << Connection: keep-alive");
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << Content-Length: {contentLength}");
            if (contentLength > 0 && content != null)
            {
                Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {remoteEndPoint} << Content-Type: text/html; charset=utf-8");
            }
#endif
            await stream.WriteLineAsync($"HTTP/1.1 {code_and_message}");
            if (isKeepAlive) await stream.WriteLineAsync("Connection: keep-alive");
            await stream.WriteLineAsync($"Content-Length: {contentLength}");
            if (contentLength > 0 && content != null)
            {
                await stream.WriteLineAsync($"Content-Type: text/html; charset=utf-8");
            }
            await stream.WriteLineAsync();
            if (contentLength > 0 && content != null)
            {
                await stream.WriteAsync(content, 0, contentLength);
            }
            await stream.FlushAsync();
            return isKeepAlive;
        }
    }
}
