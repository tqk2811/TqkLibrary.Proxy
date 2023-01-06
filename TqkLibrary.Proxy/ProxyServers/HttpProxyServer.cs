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

        protected override async Task ProxyWorkAsync(Stream client_stream, EndPoint client_EndPoint, CancellationToken cancellationToken = default)
        {
            bool client_isKeepAlive = false;
            bool should_continue = false;
            do
            {
                should_continue = false;
                List<string> client_HeaderLines = await client_stream.ReadHeader();
                if (client_HeaderLines.Count == 0)
                    return;//client stream closed
#if DEBUG
                client_HeaderLines.ForEach(x => Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} -> {x}"));
#endif
                HeaderRequestParse client_HeaderParse = client_HeaderLines.ParseRequest();

                //Check Proxy-Authorization
                if (Credentials != null)
                {
                    if (client_HeaderParse.ProxyAuthorization == null)
                    {
                        //must read content if post,...
                        await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength);
                        should_continue = await WriteResponse407(client_EndPoint, client_stream);
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
                                            await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength);
                                            should_continue = await WriteResponse407(client_EndPoint, client_stream);
                                            continue;
                                        }
                                        //else work
                                    }
                                    break;
                                }

                            default:
                                //must read content if post,...
                                await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength);
                                should_continue = await WriteResponse(client_EndPoint, client_stream, true, "400 Bad Request");
                                continue;
                        }
                    }
                }

                client_isKeepAlive = client_HeaderParse.IsKeepAlive;

                if ("CONNECT".Equals(client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                {
                    should_continue = await HttpsTransfer(
                        client_EndPoint,
                        client_stream,
                        client_HeaderParse);
                }
                else
                {
                    should_continue = await HttpTransfer(
                        client_EndPoint,
                        client_stream,
                        client_HeaderLines,
                        client_HeaderParse);
                }
            }
            while ((client_isKeepAlive || should_continue));
        }


        async Task<bool> HttpsTransfer(
            EndPoint client_EndPoint,
            Stream client_stream,
            HeaderRequestParse client_HeaderParse)
        {
            using IConnectionSource connectionSource = await this.ProxySource.InitConnectionAsync(client_HeaderParse.Uri);
            if (connectionSource == null)
            {
                //must read content if post,...
                await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength);
                return await WriteResponse(client_EndPoint, client_stream, true, "408 Request Timeout");
            }
            else
            {
                await WriteResponse(client_EndPoint, client_stream, true, "200 Connection established");
            }

            using var remote_stream = connectionSource.GetStream();
            await new StreamTransferHelper(client_stream, remote_stream)
#if DEBUG
                .DebugName(client_EndPoint.ToString(), client_HeaderParse.Uri.ToString())
#endif
                .WaitUntilDisconnect();
            return true;
        }

        async Task<bool> HttpTransfer(
            EndPoint client_EndPoint,
            Stream client_stream,
            List<string> client_HeaderLines,
            HeaderRequestParse client_HeaderParse)
        {
            //raw http header request
            using IConnectionSource connectionSource = await this.ProxySource.InitConnectionAsync(client_HeaderParse.Uri);
            if (connectionSource is null)
            {
                return await WriteResponse(client_EndPoint, client_stream, true, "408 Request Timeout");
            }
            using Stream target_Stream = connectionSource.GetStream();


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
                Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_HeaderParse.Uri.Host} <- {line}");
#endif
            }
            await target_Stream.WriteLineAsync();


            //Transfer content from client to target if have
            await client_stream.TransferAsync(target_Stream, client_HeaderParse.ContentLength);
#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] [{client_EndPoint} -> {client_HeaderParse.Uri.Host}] {client_HeaderParse.ContentLength} bytes");
#endif
            await target_Stream.FlushAsync();


            //-----------------------------------------------------
            //read header from target, and send back to client
            List<string> target_response_HeaderLines = await target_Stream.ReadHeader();
            int ContentLength = target_response_HeaderLines.GetContentLength();
            foreach (var line in target_response_HeaderLines)
            {
                await client_stream.WriteLineAsync(line);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_HeaderParse.Uri.Host} -> {line}");
#endif
            }
            await client_stream.WriteLineAsync();


            //Transfer content from target to client if have
            await target_Stream.TransferAsync(client_stream, ContentLength);
#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] [{client_EndPoint} <- {client_HeaderParse.Uri.Host}] {ContentLength} bytes");
#endif
            await client_stream.FlushAsync();

            return true;
        }

        async Task<bool> WriteResponse407(EndPoint client_EndPoint, Stream client_stream)
        {
#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- HTTP/1.1 407 Proxy Authentication Required");
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- Proxy-Authenticate: Basic Scheme='Data'");
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- Connection: keep-alive");
#endif
            await client_stream.WriteLineAsync($"HTTP/1.1 407 Proxy Authentication Required");
            await client_stream.WriteLineAsync("Proxy-Authenticate: Basic Scheme='Data'");
            await client_stream.WriteLineAsync("Connection: keep-alive");
            await client_stream.WriteLineAsync();
            await client_stream.FlushAsync();
            return true;
        }

        async Task<bool> WriteResponse(EndPoint client_EndPoint, Stream client_stream, bool isKeepAlive, string code_and_message, string content_message = null)
        {
            int contentLength = 0;
            byte[] content = null;
            if (!string.IsNullOrWhiteSpace(content_message))
            {
                content = Encoding.UTF8.GetBytes(content_message);
                contentLength = content.Length;
            }

#if DEBUG
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- HTTP/1.1 {code_and_message}");
            if (isKeepAlive) Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- Connection: keep-alive");
            Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- Content-Length: {contentLength}");
            if (contentLength > 0 && content != null)
            {
                Console.WriteLine($"[{nameof(HttpProxyServer)}.{nameof(ProxyWork)}] {client_EndPoint} <- Content-Type: text/html; charset=utf-8");
            }
#endif
            await client_stream.WriteLineAsync($"HTTP/1.1 {code_and_message}");
            if (isKeepAlive) await client_stream.WriteLineAsync("Connection: keep-alive");
            await client_stream.WriteLineAsync($"Content-Length: {contentLength}");
            if (contentLength > 0 && content != null)
            {
                await client_stream.WriteLineAsync($"Content-Type: text/html; charset=utf-8");
            }
            await client_stream.WriteLineAsync();
            if (contentLength > 0 && content != null)
            {
                await client_stream.WriteAsync(content, 0, contentLength);
            }
            await client_stream.FlushAsync();
            return isKeepAlive;
        }
    }
}
