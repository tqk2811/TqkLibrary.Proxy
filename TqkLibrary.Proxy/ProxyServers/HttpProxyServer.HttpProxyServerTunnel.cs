using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class HttpProxyServer
    {
        class HttpProxyServerTunnel
        {
            readonly HttpProxyServer httpProxyServer;
            readonly Stream client_stream;
            readonly EndPoint client_EndPoint;
            readonly CancellationToken cancellationToken;
            internal HttpProxyServerTunnel(HttpProxyServer httpProxyServer, Stream client_stream, EndPoint client_EndPoint, CancellationToken cancellationToken = default)
            {
                this.httpProxyServer = httpProxyServer ?? throw new ArgumentNullException(nameof(httpProxyServer));
                this.client_stream = client_stream ?? throw new ArgumentNullException(nameof(client_stream));
                this.client_EndPoint = client_EndPoint ?? throw new ArgumentNullException(nameof(client_EndPoint));
                this.cancellationToken = cancellationToken;
            }

            List<string> client_HeaderLines;
            HeaderRequestParse client_HeaderParse;

            internal async Task ProxyWorkAsync()
            {
                bool client_isKeepAlive = false;
                bool should_continue = false;
                do
                {
                    should_continue = false;

                    client_HeaderLines = await client_stream.ReadHeader(cancellationToken);
                    if (client_HeaderLines.Count == 0)
                        return;//client stream closed

#if DEBUG
                    client_HeaderLines.ForEach(x => Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} -> {x}"));
#endif
                    client_HeaderParse = client_HeaderLines.ParseRequest();

                    //Check Proxy-Authorization
                    if (httpProxyServer.Credentials != null)
                    {
                        if (client_HeaderParse.ProxyAuthorization == null)
                        {
                            //must read content if post,...
                            await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength, cancellationToken);
                            should_continue = await WriteResponse407();
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
                                            if (!split[0].Equals(httpProxyServer.Credentials.UserName, StringComparison.OrdinalIgnoreCase) ||
                                                !split[1].Equals(httpProxyServer.Credentials.Password, StringComparison.OrdinalIgnoreCase))
                                            {
                                                //must read content if post,...
                                                await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength, cancellationToken);
                                                should_continue = await WriteResponse407();
                                                continue;
                                            }
                                            //else work
                                        }
                                        break;
                                    }

                                default:
                                    //must read content if post,...
                                    await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength, cancellationToken);
                                    should_continue = await WriteResponse(true, "400 Bad Request");
                                    continue;
                            }
                        }
                    }

                    client_isKeepAlive = client_HeaderParse.IsKeepAlive;

                    if ("CONNECT".Equals(client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        should_continue = await HttpsTransfer();
                    }
                    else
                    {
                        should_continue = await HttpTransfer();
                    }
                }
                while ((client_isKeepAlive || should_continue));
            }

            async Task<bool> HttpsTransfer()
            {
                using IConnectionSource connectionSource = await httpProxyServer.ProxySource.InitConnectionAsync(client_HeaderParse.Uri, cancellationToken);
                if (connectionSource == null)
                {
                    //must read content if post,...
                    await client_stream.ReadBytesAsync(client_HeaderParse.ContentLength, cancellationToken);
                    return await WriteResponse(true, "408 Request Timeout");
                }
                else
                {
                    await WriteResponse(true, "200 Connection established");
                }

                using var remote_stream = connectionSource.GetStream();
                await new StreamTransferHelper(client_stream, remote_stream)
#if DEBUG
                    .DebugName(client_EndPoint.ToString(), client_HeaderParse.Uri.ToString())
#endif
                    .WaitUntilDisconnect(cancellationToken);
                return true;
            }

            async Task<bool> HttpTransfer()
            {
                //raw http header request
                using IConnectionSource connectionSource = await httpProxyServer.ProxySource.InitConnectionAsync(client_HeaderParse.Uri, cancellationToken);
                if (connectionSource is null)
                {
                    return await WriteResponse(true, "408 Request Timeout");
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
                    await target_Stream.WriteLineAsync(line, cancellationToken);
#if DEBUG
                    Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_HeaderParse.Uri.Host} <- {line}");
#endif
                }
                await target_Stream.WriteLineAsync(cancellationToken);


                //Transfer content from client to target if have
                await client_stream.TransferAsync(target_Stream, client_HeaderParse.ContentLength, cancellationToken: cancellationToken);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] [{client_EndPoint} -> {client_HeaderParse.Uri.Host}] {client_HeaderParse.ContentLength} bytes");
#endif
                await target_Stream.FlushAsync(cancellationToken);


                //-----------------------------------------------------
                //read header from target, and send back to client
                List<string> target_response_HeaderLines = await target_Stream.ReadHeader(cancellationToken);
                int ContentLength = target_response_HeaderLines.GetContentLength();
                foreach (var line in target_response_HeaderLines)
                {
                    await client_stream.WriteLineAsync(line, cancellationToken);
#if DEBUG
                    Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_HeaderParse.Uri.Host} -> {line}");
#endif
                }
                await client_stream.WriteLineAsync(cancellationToken);


                //Transfer content from target to client if have
                await target_Stream.TransferAsync(client_stream, ContentLength, cancellationToken: cancellationToken);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] [{client_EndPoint} <- {client_HeaderParse.Uri.Host}] {ContentLength} bytes");
#endif
                await client_stream.FlushAsync(cancellationToken);

                return true;
            }

            async Task<bool> WriteResponse407()
            {
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- HTTP/1.1 407 Proxy Authentication Required");
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- Proxy-Authenticate: Basic Scheme='Data'");
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- Connection: keep-alive");
#endif
                await client_stream.WriteLineAsync($"HTTP/1.1 407 Proxy Authentication Required", cancellationToken);
                await client_stream.WriteLineAsync("Proxy-Authenticate: Basic Scheme='Data'", cancellationToken);
                await client_stream.WriteLineAsync("Connection: keep-alive", cancellationToken);
                await client_stream.WriteLineAsync(cancellationToken);
                await client_stream.FlushAsync(cancellationToken);
                return true;
            }

            async Task<bool> WriteResponse(
                bool isKeepAlive,
                string code_and_message,
                string content_message = null)
            {
                int contentLength = 0;
                byte[] content = null;
                if (!string.IsNullOrWhiteSpace(content_message))
                {
                    content = Encoding.UTF8.GetBytes(content_message);
                    contentLength = content.Length;
                }

#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- HTTP/1.1 {code_and_message}");
                if (isKeepAlive) Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- Connection: keep-alive");
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- Content-Length: {contentLength}");
                if (contentLength > 0 && content != null)
                {
                    Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {client_EndPoint} <- Content-Type: text/html; charset=utf-8");
                }
#endif
                await client_stream.WriteLineAsync($"HTTP/1.1 {code_and_message}", cancellationToken);
                if (isKeepAlive) await client_stream.WriteLineAsync("Connection: keep-alive", cancellationToken);
                await client_stream.WriteLineAsync($"Content-Length: {contentLength}", cancellationToken);
                if (contentLength > 0 && content != null)
                {
                    await client_stream.WriteLineAsync($"Content-Type: text/html; charset=utf-8", cancellationToken);
                }
                await client_stream.WriteLineAsync(cancellationToken);
                if (contentLength > 0 && content != null)
                {
                    await client_stream.WriteAsync(content, cancellationToken);
                }
                await client_stream.FlushAsync(cancellationToken);
                return isKeepAlive;
            }
        }
    }
}
