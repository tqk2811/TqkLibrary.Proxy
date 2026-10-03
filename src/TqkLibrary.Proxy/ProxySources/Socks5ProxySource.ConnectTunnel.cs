using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        public class ConnectTunnel : BaseTunnel, IConnectSource
        {
            internal protected ConnectTunnel(Socks5ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }

            public virtual async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));
                CheckIsDisposed();

                using var scope = _logger?.BeginScope("TunnelId:{TunnelId} TargetHost:{TargetHost}", _tunnelId, $"{address.Host}:{address.Port}");

                await base.ConnectAndAuthAsync(cancellationToken);

                Socks5_Request socks5_Connection = Socks5_Request.CreateConnect(address);
                _logger?.LogInformation("CONNECT request");
                byte[] request = socks5_Connection.GetByteArray();
                if (_proxySource.ConnectRequestChunkSize > 0)
                {
                    // NoDelay keeps the stack from coalescing the small chunks; restored so the
                    // tunnelled data is sent the way it always was.
                    bool noDelay = _tcpClient.NoDelay;
                    _tcpClient.NoDelay = true;
                    try
                    {
                        // Only the address is trickled out: VER CMD RSV ATYP (and the name's length
                        // byte) in one write, the address chunked, the port in one write.
                        int addressStart = request[3] == 0x03 ? 5 : 4;
                        await _stream!.WriteSplitAroundAsync(request, 0, request.Length, addressStart, request.Length - addressStart - 2, _proxySource.ConnectRequestChunkSize, cancellationToken);
                    }
                    finally
                    {
                        _tcpClient.NoDelay = noDelay;
                    }
                }
                else
                {
                    await _stream!.WriteAsync(request, cancellationToken);
                    await _stream!.FlushAsync(cancellationToken);
                }
                Socks5_RequestResponse socks5_RequestResponse = await _stream!.Read_Socks5_RequestResponse_Async(cancellationToken);
                if (socks5_RequestResponse.STATUS != Socks5_STATUS.RequestGranted)
                {
                    _logger?.LogWarning("CONNECT REJECTED status={Status}", socks5_RequestResponse.STATUS);
                    throw new InitConnectSourceFailedException($"{nameof(Socks5_STATUS)}: {socks5_RequestResponse.STATUS}");
                }
                _logger?.LogInformation("CONNECT OK bnd={BndAddress}:{BndPort}", socks5_RequestResponse.BNDADDR.IPAddress, socks5_RequestResponse.BNDPORT);

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
        }
    }
}
