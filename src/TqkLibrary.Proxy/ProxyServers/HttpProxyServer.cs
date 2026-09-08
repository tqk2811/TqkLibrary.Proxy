using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class HttpProxyServer : IProxyServer, IHttpProxy
    {
        readonly ILoggerFactory? _loggerFactory;
        readonly ILogger? _logger;

        Stream? _clientStream;
        IPEndPoint? _clientEndPoint;
        IProxyServerHandler? _proxyServerHandler;
        Guid _tunnelId;
        CancellationToken _cancellationToken;


        IReadOnlyList<string>? _client_HeaderLines = null;
        HeaderRequestParse? _client_HeaderParse = null;

        public HttpProxyServer(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<HttpProxyServer>();
        }

        public async Task ProxyWorkAsync(
            Stream clientStream,
            IPEndPoint clientEndPoint,
            IProxyServerHandler proxyServerHandler,
            Guid tunnelId,
            CancellationToken cancellationToken = default
            )
        {
            if (_clientStream is not null)
                throw new InvalidOperationException($"Please create new instance of {nameof(HttpProxyServer)} per connection");

            _clientStream = clientStream;
            _clientEndPoint = clientEndPoint;
            _proxyServerHandler = proxyServerHandler;
            _tunnelId = tunnelId;
            _cancellationToken = cancellationToken;


            bool client_isKeepAlive = false;
            bool should_continue = false;


            do
            {
                should_continue = false;

                _client_HeaderLines = await _clientStream.ReadHeadersAsync(_cancellationToken);
                if (_client_HeaderLines.Count == 0)
                    return;//client stream closed

                _logger?.LogInformation("Client request headers\r\n{Headers}", string.Join("\r\n", _client_HeaderLines));

                _client_HeaderParse = HeaderRequestParse.ParseRequest(_client_HeaderLines);

                BaseUserInfo userInfo = new BaseUserInfo(clientEndPoint, tunnelId);

                if ("basic".Equals(_client_HeaderParse.ProxyAuthorization?.Scheme?.ToLower(), StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(_client_HeaderParse?.ProxyAuthorization?.Parameter))
                        throw new InvalidOperationException($"ProxyAuthorization Parameter is empty");

                    string parameter = Encoding.UTF8.GetString(Convert.FromBase64String(_client_HeaderParse!.ProxyAuthorization!.Parameter!));
                    string[] split = parameter.Split(':');
                    if (split.Length == 2)
                    {
                        userInfo.Authentication = new ProxyCredential(split[0], split[1]);
                    }
                    else throw new InvalidOperationException($"ProxyAuthorization Parameter is wrong data '{parameter}'");
                }

                //Check Proxy-Authorization
                if (!await proxyServerHandler.IsAcceptUserAsync(userInfo, cancellationToken))
                {
                    //must read content if post,...
                    await _clientStream.ReadBytesAsync(_client_HeaderParse.ContentLength, _cancellationToken);
                    should_continue = await _WriteResponse407();
                    continue;
                }

                client_isKeepAlive = _client_HeaderParse.IsKeepAlive;

                if (await proxyServerHandler.IsAcceptDomainAsync(_client_HeaderParse.Uri, userInfo, cancellationToken))
                {
                    IProxySource proxySource = await proxyServerHandler.GetProxySourceAsync(_client_HeaderParse.Uri, userInfo, cancellationToken);
                    using IConnectSource connectSource = await proxySource.GetConnectSourceAsync(tunnelId);
                    try
                    {
                        await connectSource.ConnectAsync(_client_HeaderParse.Uri, _cancellationToken);
                        using Stream source_stream = await connectSource.GetStreamAsync();
                        if ("CONNECT".Equals(_client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                        {
                            should_continue = await _HttpsTransfer(source_stream, userInfo);
                        }
                        else
                        {
                            should_continue = await _HttpTransfer(source_stream, userInfo);
                        }
                    }
                    catch (InitConnectSourceFailedException ex)
                    {
                        _logger?.LogInformation(ex, "InitConnectSource failed");
                        await _WriteResponse((int)HttpStatusCode.ServiceUnavailable, "Service Unavailable", true);
                    }
                }
                else
                {
                    await _WriteResponse(403, "Forbidden", true);
                    should_continue = client_isKeepAlive;
                }
            }
            while (client_isKeepAlive && should_continue);
        }


        async Task<bool> _HttpsTransfer(Stream source_stream, IUserInfo userInfo)
        {
            if (_client_HeaderParse is null)
                throw new InvalidOperationException();

            await _WriteResponse(200, "Connection established", true);

            using Stream clientStream = await _proxyServerHandler!.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);

            await new StreamTransferHelper(clientStream, source_stream, _tunnelId, _loggerFactory)
                .DebugName(_clientEndPoint, _client_HeaderParse?.Uri)
                .WaitUntilDisconnect(_cancellationToken);
            return false;
        }

        async Task<bool> _HttpTransfer(Stream source_stream, IUserInfo userInfo)
        {
            if (_client_HeaderParse is null || _client_HeaderLines is null)
                throw new InvalidOperationException();

            //send header to target
            List<string> headerLines = new List<string>();
            // PathAndQuery, not AbsolutePath: the query string is part of what was asked for, and
            // dropping it turned every search, every paged list and every signed URL into a request
            // for the bare path.
            headerLines.Add($"{_client_HeaderParse!.Method} {_client_HeaderParse.Uri!.PathAndQuery} HTTP/{_client_HeaderParse!.Version}");
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
            _logger?.LogInformation("Sending headers to {TargetHost}\r\n{Headers}", _client_HeaderParse.Uri.Host, string.Join("\r\n", headerLines));

            await source_stream.WriteLineAsync(_cancellationToken);

            // NOT disposed unless the handler actually built something. The default one hands back
            // the very stream it was given, and disposing that closed the connection to the client
            // at the end of the first request — so keep-alive was dead from the second request
            // onwards on every deployment that had not overridden the handler.
            Stream clientStream = await _proxyServerHandler!.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);
            bool ownsClientStream = !ReferenceEquals(clientStream, _clientStream);
            try
            {
                //Transfer content from client to target if have
                await clientStream.TransferAsync(source_stream, _client_HeaderParse.ContentLength, cancellationToken: _cancellationToken);
                _logger?.LogInformation("Sent {Bytes} bytes -> {TargetHost}", _client_HeaderParse.ContentLength, _client_HeaderParse.Uri.Host);

                await source_stream.FlushAsync(_cancellationToken);

                //-----------------------------------------------------
                //read header from target, and send back to client
                IReadOnlyList<string> target_response_HeaderLines = await source_stream.ReadHeadersAsync(_cancellationToken);

                await clientStream.WriteLineAsync(string.Join("\r\n", target_response_HeaderLines), _cancellationToken);
                _logger?.LogInformation("Received headers from {TargetHost}\r\n{Headers}", _client_HeaderParse.Uri.Host, string.Join("\r\n", target_response_HeaderLines));

                await clientStream.WriteLineAsync(_cancellationToken);

                // Through the handler's stream, not straight down _clientStream: a handler that
                // counts bytes or shapes traffic was being bypassed for the whole response body.
                bool reusable = await _TransferResponseBodyAsync(source_stream, clientStream, target_response_HeaderLines);

                await clientStream.FlushAsync(_cancellationToken);

                return reusable;
            }
            finally
            {
                if (ownsClientStream) clientStream.Dispose();
            }
        }

        /// <summary>
        /// Forwards a response body in whichever of the three shapes HTTP/1.1 allows, and answers
        /// whether this connection can carry another request afterwards.
        /// </summary>
        /// <remarks>
        /// Only the first shape used to be handled — a Content-Length. A chunked response has none,
        /// and GetContentLength answered zero for it, so the body was never forwarded at all: the
        /// client received the headers and then nothing, which is what any streamed page looks like
        /// through this proxy. A response delimited by the connection closing fared the same.
        /// </remarks>
        async Task<bool> _TransferResponseBodyAsync(Stream from, Stream to, IReadOnlyList<string> responseHeaderLines)
        {
            if (!_ResponseCanHaveBody(responseHeaderLines)) return true;

            if (responseHeaderLines.IsChunked())
            {
                await from.TransferChunkedAsync(to, _cancellationToken);
                return true;
            }

            if (responseHeaderLines.HasContentLength())
            {
                int contentLength = responseHeaderLines.GetContentLength();
                await from.TransferAsync(to, contentLength, cancellationToken: _cancellationToken);
                _logger?.LogInformation("Received {Bytes} bytes <- {TargetHost}", contentLength, _client_HeaderParse!.Uri!.Host);
                return true;
            }

            // Neither a length nor chunks: the body IS the rest of the connection, and the only
            // thing that ends it is the server closing. Nothing more can be sent over this one.
            await from.CopyToAsync(to, 81920, _cancellationToken);
            return false;
        }

        /// <summary>Whether a response of this shape carries a body at all.</summary>
        /// <remarks>
        /// Without this a 204 or a 304 — neither of which may have one — would be read as
        /// close-delimited and the proxy would sit waiting for a body the server is never going to
        /// send, holding the client's request open until something timed out.
        /// </remarks>
        bool _ResponseCanHaveBody(IReadOnlyList<string> responseHeaderLines)
        {
            if ("HEAD".Equals(_client_HeaderParse?.Method, StringComparison.OrdinalIgnoreCase)) return false;

            int status = responseHeaderLines.GetStatusCode();
            return status != 204 && status != 304 && !(status >= 100 && status < 200);
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

            await _clientStream!.WriteHeadersAsync(headers, _cancellationToken);
            _logger?.LogInformation("Sent response headers\r\n{Headers}", string.Join("\r\n", headers));

            if (body is not null)
            {
                await _clientStream!.WriteAsync(body, _cancellationToken);
                _logger?.LogInformation("Sent response body {Bytes} bytes", body.Length);
            }

            await _clientStream!.FlushAsync(_cancellationToken);

            return headers.Any(x => x.Equals("Proxy-Connection: keep-alive", StringComparison.OrdinalIgnoreCase));
        }
    }
}
