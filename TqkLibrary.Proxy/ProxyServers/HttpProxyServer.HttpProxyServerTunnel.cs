using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class HttpProxyServer
    {
        class HttpProxyServerTunnel : BaseProxyServerTunnel<HttpProxyServer>
        {
            internal HttpProxyServerTunnel(
                HttpProxyServer proxyServer,
                Stream clientStream,
                EndPoint clientEndPoint,
                CancellationToken cancellationToken = default
                )
                : base(
                      proxyServer,
                      clientStream,
                      clientEndPoint,
                      cancellationToken
                      )
            {
            }

            List<string> _client_HeaderLines;
            HeaderRequestParse _client_HeaderParse;

            internal override async Task ProxyWorkAsync()
            {
                bool client_isKeepAlive = false;
                bool should_continue = false;
                do
                {
                    should_continue = false;

                    _client_HeaderLines = await _clientStream.ReadHeader(_cancellationToken);
                    if (_client_HeaderLines.Count == 0)
                        return;//client stream closed

#if DEBUG
                    _client_HeaderLines.ForEach(x => Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} -> {x}"));
#endif
                    _client_HeaderParse = _client_HeaderLines.ParseRequest();

                    //Check Proxy-Authorization
                    if (await _proxyServer.HttpProxyServerFilter.IsNeedAuthenticationAsync(_cancellationToken))
                    {
                        switch (_client_HeaderParse.ProxyAuthorization?.Scheme?.ToLower())
                        {
                            case "basic":
                                string parameter = Encoding.UTF8.GetString(Convert.FromBase64String(_client_HeaderParse.ProxyAuthorization.Parameter));
                                string[] split = parameter.Split(':');
                                if (split.Length == 2 &&
                                    split.All(x => !string.IsNullOrWhiteSpace(x)) &&
                                    await _proxyServer.HttpProxyServerFilter.CheckAuthenticationAsync(new HttpProxyAuthentication(split[0], split[1]), _cancellationToken))
                                {
                                    break;//allow
                                }
                                //must read content if post,...
                                await _clientStream.ReadBytesAsync(_client_HeaderParse.ContentLength, _cancellationToken);
                                should_continue = await _WriteResponse407();
                                continue;

                            case null:
                                //must read content if post,...
                                await _clientStream.ReadBytesAsync(_client_HeaderParse.ContentLength, _cancellationToken);
                                should_continue = await _WriteResponse407();
                                continue;

                            default:
                                //must read content if post,...
                                await _clientStream.ReadBytesAsync(_client_HeaderParse.ContentLength, _cancellationToken);
                                should_continue = await _WriteResponse(true, "400 Bad Request");
                                continue;
                        }
                    }

                    client_isKeepAlive = _client_HeaderParse.IsKeepAlive;

                    if ("CONNECT".Equals(_client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        should_continue = await _HttpsTransfer();
                    }
                    else
                    {
                        should_continue = await _HttpTransfer();
                    }
                }
                while ((client_isKeepAlive || should_continue));
            }

            async Task<bool> _HttpsTransfer()
            {
                using IConnectSource connectSource = await _proxyServer.ProxySource.InitConnectAsync(_client_HeaderParse.Uri, _cancellationToken);
                if (connectSource == null)
                {
                    //must read content if post,...
                    await _clientStream.ReadBytesAsync(_client_HeaderParse.ContentLength, _cancellationToken);
                    return await _WriteResponse(true, "408 Request Timeout");
                }
                else
                {
                    await _WriteResponse(true, "200 Connection established");
                }

                using var remote_stream = connectSource.GetStream();
                await new StreamTransferHelper(_clientStream, remote_stream)
#if DEBUG
                    .DebugName(_clientEndPoint.ToString(), _client_HeaderParse.Uri.ToString())
#endif
                    .WaitUntilDisconnect(_cancellationToken);
                return true;
            }

            async Task<bool> _HttpTransfer()
            {
                //raw http header request
                using IConnectSource connectSource = await _proxyServer.ProxySource.InitConnectAsync(_client_HeaderParse.Uri, _cancellationToken);
                if (connectSource is null)
                {
                    return await _WriteResponse(true, "408 Request Timeout");
                }
                using Stream target_Stream = connectSource.GetStream();


                //send header to target
                List<string> headerLines = new List<string>();
                headerLines.Add($"{_client_HeaderParse.Method} {_client_HeaderParse.Uri.AbsolutePath} HTTP/{_client_HeaderParse.Version}");
                if (!_client_HeaderLines.Any(x => x.StartsWith("host: ", StringComparison.OrdinalIgnoreCase)))
                {
                    headerLines.Add($"Host: {_client_HeaderParse.Uri.Host}");
                }
                foreach (var line in _client_HeaderLines.Skip(1)
                    .Where(x => !x.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase)))
                {
                    headerLines.Add(line);
                }

                foreach (var line in headerLines)
                {
                    await target_Stream.WriteLineAsync(line, _cancellationToken);
#if DEBUG
                    Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_client_HeaderParse.Uri.Host} <- {line}");
#endif
                }
                await target_Stream.WriteLineAsync(_cancellationToken);


                //Transfer content from client to target if have
                await _clientStream.TransferAsync(target_Stream, _client_HeaderParse.ContentLength, cancellationToken: _cancellationToken);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] [{_clientEndPoint} -> {_client_HeaderParse.Uri.Host}] {_client_HeaderParse.ContentLength} bytes");
#endif
                await target_Stream.FlushAsync(_cancellationToken);


                //-----------------------------------------------------
                //read header from target, and send back to client
                List<string> target_response_HeaderLines = await target_Stream.ReadHeader(_cancellationToken);
                int ContentLength = target_response_HeaderLines.GetContentLength();
                foreach (var line in target_response_HeaderLines)
                {
                    await _clientStream.WriteLineAsync(line, _cancellationToken);
#if DEBUG
                    Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_client_HeaderParse.Uri.Host} -> {line}");
#endif
                }
                await _clientStream.WriteLineAsync(_cancellationToken);


                //Transfer content from target to client if have
                await target_Stream.TransferAsync(_clientStream, ContentLength, cancellationToken: _cancellationToken);
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] [{_clientEndPoint} <- {_client_HeaderParse.Uri.Host}] {ContentLength} bytes");
#endif
                await _clientStream.FlushAsync(_cancellationToken);

                return true;
            }

            async Task<bool> _WriteResponse407()
            {
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- HTTP/1.1 407 Proxy Authentication Required");
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- Proxy-Authenticate: Basic Scheme='Data'");
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- Connection: keep-alive");
#endif
                await _clientStream.WriteLineAsync($"HTTP/1.1 407 Proxy Authentication Required", _cancellationToken);
                await _clientStream.WriteLineAsync("Proxy-Authenticate: Basic Scheme='Data'", _cancellationToken);
                await _clientStream.WriteLineAsync("Connection: keep-alive", _cancellationToken);
                await _clientStream.WriteLineAsync(_cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
                return true;
            }

            async Task<bool> _WriteResponse(
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
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- HTTP/1.1 {code_and_message}");
                if (isKeepAlive) Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- Connection: keep-alive");
                Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- Content-Length: {contentLength}");
                if (contentLength > 0 && content != null)
                {
                    Console.WriteLine($"[{nameof(HttpProxyServerTunnel)}.{nameof(ProxyWorkAsync)}] {_clientEndPoint} <- Content-Type: text/html; charset=utf-8");
                }
#endif
                await _clientStream.WriteLineAsync($"HTTP/1.1 {code_and_message}", _cancellationToken);
                if (isKeepAlive) await _clientStream.WriteLineAsync("Connection: keep-alive", _cancellationToken);
                await _clientStream.WriteLineAsync($"Content-Length: {contentLength}", _cancellationToken);
                if (contentLength > 0 && content != null)
                {
                    await _clientStream.WriteLineAsync($"Content-Type: text/html; charset=utf-8", _cancellationToken);
                }
                await _clientStream.WriteLineAsync(_cancellationToken);
                if (contentLength > 0 && content != null)
                {
                    await _clientStream.WriteAsync(content, _cancellationToken);
                }
                await _clientStream.FlushAsync(_cancellationToken);
                return isKeepAlive;
            }
        }
    }
}
