using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource
    {
        public class ConnectTunnel : BaseProxySourceTunnel<HttpProxySource>, IConnectSource
        {
            protected readonly ILogger? _logger;

            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream? _stream;
            internal protected ConnectTunnel(HttpProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {
                _logger = proxySource._loggerFactory?.CreateLogger(GetType());
            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _stream = null;
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            public virtual async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));
                CheckIsDisposed();
#if NET5_0_OR_GREATER
                await _tcpClient.ConnectAsync(_proxySource._proxy.Host, _proxySource._proxy.Port, cancellationToken);
#else
                await _tcpClient.ConnectAsync(_proxySource._proxy.Host, _proxySource._proxy.Port);
#endif
                _stream = _tcpClient.GetStream();

                if (!await _CONNECT_Async(address, cancellationToken))
                {
                    throw new InitConnectSourceFailedException();
                }

                // Wrapped only once CONNECT is through: the request above is the proxy's business,
                // the bytes from here on are the tunnel's.
                if (_proxySource.TlsHandshakeChunkSize > 0)
                    _stream = new TlsHandshakeChunkingStream(_stream!, _proxySource.TlsHandshakeChunkSize, _tcpClient.Client);
            }
            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectTunnel)}.{nameof(ConnectAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(_stream);
            }

            protected virtual async Task<bool> _CONNECT_Async(Uri address, CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException();

                using var scope = _logger?.BeginScope("TunnelId:{TunnelId} UpstreamProxy:{UpstreamProxy} TargetHost:{TargetHost}", _tunnelId, $"{_proxySource._proxy.Host}:{_proxySource._proxy.Port}", $"{address.Host}:{address.Port}");

                List<string> headers = new List<string>();
                headers.Add($"CONNECT {address.Host}:{address.Port} HTTP/1.1");
                if (_proxySource.Credential is not null)
                {
                    string data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_proxySource.Credential.UserName}:{_proxySource.Credential.Password}"));
                    headers.Add($"Proxy-Authorization: Basic {data}");
                }

                if (_proxySource.ConnectRequestChunkSize > 0)
                    await _WriteSplitHeadersAsync(headers, cancellationToken);
                else
                    await _stream.WriteHeadersAsync(headers, cancellationToken);
                _logger?.LogInformation("Sending CONNECT to upstream\r\n{Headers}", string.Join("\r\n", headers));

                await _stream.FlushAsync(cancellationToken);

                //-----------------------///

                IReadOnlyList<string> response_HeaderLines = await _stream.ReadHeadersAsync(cancellationToken);
                _logger?.LogInformation("Upstream CONNECT response\r\n{Headers}", string.Join("\r\n", response_HeaderLines));

                var headerResponseParse = HeaderResponseParse.ParseResponse(response_HeaderLines);

                return headerResponseParse.HttpStatusCode == HttpStatusCode.OK;
            }

            // Same bytes WriteHeadersAsync puts on the wire, but the request line goes out in small chunks,
            // a segment each, and the rest, from its CRLF on, in one write. NoDelay is what keeps the stack
            // from holding the small chunks back to coalesce them; it goes back afterwards so the
            // tunnelled data is sent the way it always was.
            protected virtual async Task _WriteSplitHeadersAsync(IReadOnlyList<string> headers, CancellationToken cancellationToken)
            {
                byte[] buffer = Encoding.ASCII.GetBytes(string.Join("\r\n", headers) + "\r\n\r\n");
                int requestLineLength = headers[0].Length;
                bool noDelay = _tcpClient.NoDelay;
                _tcpClient.NoDelay = true;
                try
                {
                    await _stream!.WriteInChunksAsync(buffer, 0, requestLineLength, _proxySource.ConnectRequestChunkSize, cancellationToken);
                    await _stream!.WriteAsync(buffer, requestLineLength, buffer.Length - requestLineLength, cancellationToken);
                    await _stream!.FlushAsync(cancellationToken);
                }
                finally
                {
                    _tcpClient.NoDelay = noDelay;
                }
            }

        }
    }
}
