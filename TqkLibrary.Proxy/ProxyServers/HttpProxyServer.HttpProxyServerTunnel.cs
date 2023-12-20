using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Helpers;
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

            IReadOnlyList<string>? _client_HeaderLines = null;
            HeaderRequestParse? _client_HeaderParse = null;

            internal override async Task ProxyWorkAsync()
            {
                bool client_isKeepAlive = false;
                bool should_continue = false;
                do
                {
                    should_continue = false;

                    _client_HeaderLines = await _clientStream.ReadHeadersAsync(_cancellationToken);
                    if (_client_HeaderLines.Count == 0)
                        return;//client stream closed

                    _logger?.LogInformation($"{_clientEndPoint} -> \r\n{string.Join("\r\n", _client_HeaderLines)}");

                    _client_HeaderParse = HeaderRequestParse.ParseRequest(_client_HeaderLines);

                    //Check Proxy-Authorization
                    if (await _proxyServer.Handler.IsNeedAuthenticationAsync(_cancellationToken))
                    {
                        switch (_client_HeaderParse.ProxyAuthorization?.Scheme?.ToLower())
                        {
                            case "basic":
                                if (string.IsNullOrWhiteSpace(_client_HeaderParse?.ProxyAuthorization?.Parameter))
                                    throw new InvalidDataException($"ProxyAuthorization Parameter is empty");

                                string parameter = Encoding.UTF8.GetString(Convert.FromBase64String(_client_HeaderParse!.ProxyAuthorization!.Parameter!));
                                string[] split = parameter.Split(':');
                                if (split.Length == 2 &&
                                    split.All(x => !string.IsNullOrWhiteSpace(x)) &&
                                    await _proxyServer.Handler.CheckAuthenticationAsync(new HttpProxyAuthentication(split[0], split[1]), _cancellationToken))
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
                                should_continue = await _WriteResponse(400, "Bad Request", true);
                                continue;
                        }
                    }

                    client_isKeepAlive = _client_HeaderParse.IsKeepAlive;

                    if (await _proxyServer.Handler.IsAcceptDomainFilterAsync(_client_HeaderParse.Uri, _cancellationToken))
                    {
                        using IConnectSource connectSource = _proxyServer.ProxySource.GetConnectSource();
                        await connectSource.InitAsync(_client_HeaderParse.Uri, _cancellationToken);
                        using Stream source_stream = await connectSource.GetStreamAsync();
                        if ("CONNECT".Equals(_client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                        {
                            should_continue = await _HttpsTransfer(source_stream);
                        }
                        else
                        {
                            should_continue = await _HttpTransfer(source_stream);
                        }
                    }
                    else
                    {
                        await _WriteResponse(403, "Forbidden", true);
                        should_continue = client_isKeepAlive;
                    }
                }
                while ((client_isKeepAlive || should_continue));
            }

            async Task<bool> _HttpsTransfer(Stream source_stream)
            {
                if (_client_HeaderParse is null)
                    throw new InvalidOperationException();

                await _WriteResponse(200, "Connection established", true);

                await new StreamTransferHelper(_clientStream, source_stream)
                    .DebugName(_clientEndPoint, _client_HeaderParse?.Uri)
                    .WaitUntilDisconnect(_cancellationToken);
                return false;
            }

            async Task<bool> _HttpTransfer(Stream source_stream)
            {
                if (_client_HeaderParse is null || _client_HeaderLines is null)
                    throw new InvalidOperationException();

                //send header to target
                List<string> headerLines = new List<string>();
                headerLines.Add($"{_client_HeaderParse!.Method} {_client_HeaderParse.Uri!.AbsolutePath} HTTP/{_client_HeaderParse!.Version}");
                if (!_client_HeaderLines!.Any(x => x.StartsWith("host: ", StringComparison.OrdinalIgnoreCase)))
                {
                    headerLines.Add($"Host: {_client_HeaderParse.Uri.Host}");
                }
                foreach (var line in _client_HeaderLines!.Skip(1)
                    .Where(x => !x.StartsWith("Proxy-", StringComparison.OrdinalIgnoreCase)))
                {
                    headerLines.Add(line);
                }

                await source_stream.WriteLineAsync(string.Join("\r\n", headerLines), _cancellationToken);
                _logger?.LogInformation($"{_client_HeaderParse.Uri.Host} <- \r\n{string.Join("\r\n", headerLines)}");

                await source_stream.WriteLineAsync(_cancellationToken);

                //Transfer content from client to target if have
                await _clientStream.TransferAsync(source_stream, _client_HeaderParse.ContentLength, cancellationToken: _cancellationToken);
                _logger?.LogInformation($"[{_clientEndPoint} -> {_client_HeaderParse.Uri.Host}] {_client_HeaderParse.ContentLength} bytes");

                await source_stream.FlushAsync(_cancellationToken);

                //-----------------------------------------------------
                //read header from target, and send back to client
                IReadOnlyList<string> target_response_HeaderLines = await source_stream.ReadHeadersAsync(_cancellationToken);
                int ContentLength = target_response_HeaderLines.GetContentLength();

                await _clientStream.WriteLineAsync(string.Join("\r\n", target_response_HeaderLines), _cancellationToken);
                _logger?.LogInformation($"{_client_HeaderParse.Uri.Host} ->\r\n{string.Join("\r\n", target_response_HeaderLines)}");

                await _clientStream.WriteLineAsync(_cancellationToken);

                //Transfer content from target to client if have
                await source_stream.TransferAsync(_clientStream, ContentLength, cancellationToken: _cancellationToken);
                _logger?.LogInformation($"[{_clientEndPoint} <- {_client_HeaderParse.Uri.Host}] {ContentLength} bytes");

                await _clientStream.FlushAsync(_cancellationToken);

                return true;
            }

            async Task<bool> _WriteResponse407()
            {
                return await _WriteResponse(
                    "HTTP/1.1 407 Proxy Authentication Required",
                    "Proxy-Authenticate: Basic Scheme='Data'",
                    "Proxy-Connection: keep-alive");
            }

            Task<bool> _WriteResponse(
                int code,
                string message,
                bool isKeepAlive,
                string? content = null)
            {
                List<string> headers = new List<string>();
                headers.Add($"HTTP/1.1 {code} {message}");
                if (isKeepAlive)
                    headers.Add("Proxy-Connection: keep-alive");

                byte[]? b_content = null;
                if (!string.IsNullOrWhiteSpace(content))
                {
                    headers.Add($"Content-Type: text/html; charset=utf-8");
                    b_content = Encoding.UTF8.GetBytes(content);
                }

                return _WriteResponse(headers, b_content);
            }


            /// <summary>
            /// 
            /// </summary>
            /// <param name="headers"></param>
            /// <returns>true is keep alive</returns>
            Task<bool> _WriteResponse(params string[] headers) => _WriteResponse(headers.AsEnumerable(), null);
            /// <summary>
            /// 
            /// </summary>
            /// <param name="headers"></param>
            /// <param name="body"></param>
            /// <returns>true is keep alive</returns>
            async Task<bool> _WriteResponse(IEnumerable<string> headers, byte[]? body = null)
            {
                if (body is not null && !headers.Any(x => x.StartsWith("content-length:", StringComparison.InvariantCulture)))
                {
                    headers = headers.Append($"Content-Length: {body.Length}");
                }

                await _clientStream.WriteHeadersAsync(headers, _cancellationToken);
                _logger?.LogInformation($"{_clientEndPoint} <-\r\n{string.Join("\r\n", headers)}");

                if (body is not null)
                {
                    await _clientStream.WriteAsync(body, _cancellationToken);
                    _logger?.LogInformation($"{_clientEndPoint} <- bytes {body.Length}");
                }

                await _clientStream.FlushAsync(_cancellationToken);

                return headers.Any(x => x.Equals("Proxy-Connection: keep-alive", StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
