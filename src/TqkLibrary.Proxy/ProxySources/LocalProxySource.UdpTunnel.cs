using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        // Local UDP egress channel. Unlike Socks5ProxySource.UdpTunnel (which talks to an upstream
        // SOCKS5 relay), this opens a UDP socket on the local machine and sends datagrams straight
        // to the destination — there is no SOCKS5 framing here because this *is* the source.
        public class UdpTunnel : BaseTunnel, IUdpAssociateSource
        {
            protected UdpClient? _udp;

            public IPEndPoint? RelayEndPoint { get; protected set; }
            public IPEndPoint? LocalEndPoint => _udp?.Client?.LocalEndPoint as IPEndPoint;

            internal protected UdpTunnel(LocalProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {
            }

            public virtual async Task<IPEndPoint> AssociateAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                if (_udp is not null)
                    throw new InvalidOperationException($"{nameof(AssociateAsync)} already called");

                using var scope = _logger?.BeginScope("TunnelId:{TunnelId}", _tunnelId);

                IPEndPoint listen = await _proxySource.GetListenEndPointAsync(cancellationToken);
                _udp = new UdpClient(listen);

                IPEndPoint local = (IPEndPoint)_udp.Client.LocalEndPoint!;
                IPAddress responseIp = await _proxySource.GetResponseIPAddressAsync(cancellationToken);
                RelayEndPoint = new IPEndPoint(responseIp, local.Port);

                _logger?.LogInformation("UDP source bound local={Local} relay={Relay}", local, RelayEndPoint);
                return RelayEndPoint;
            }

            public virtual async Task SendAsync(IPEndPoint destination, byte[] payload, int offset, int count, CancellationToken cancellationToken = default)
            {
                if (destination is null) throw new ArgumentNullException(nameof(destination));
                if (payload is null) throw new ArgumentNullException(nameof(payload));
                if (offset < 0 || count < 0 || offset + count > payload.Length)
                    throw new ArgumentOutOfRangeException(nameof(count));
                CheckIsDisposed();
                if (_udp is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(UdpTunnel)}.{nameof(AssociateAsync)} first");

                if (!_proxySource.IsSupportIpv6 && destination.AddressFamily == AddressFamily.InterNetworkV6)
                    throw new NotSupportedException("IpV6 are not support");

                // UdpClient.SendAsync(byte[], int, IPEndPoint) sends the first <count> bytes from the buffer —
                // we only need to copy when the caller passed a non-zero offset.
                if (offset != 0)
                {
                    byte[] slice = new byte[count];
                    Buffer.BlockCopy(payload, offset, slice, 0, count);
                    payload = slice;
                }
                await _udp.SendAsync(payload, count, destination).ConfigureAwait(false);
            }

            public virtual async Task<UdpAssociateDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                if (_udp is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(UdpTunnel)}.{nameof(AssociateAsync)} first");

#if NET6_0_OR_GREATER
                UdpReceiveResult result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                return new UdpAssociateDatagram(result.RemoteEndPoint, result.Buffer);
#else
                // netstandard2.0: ReceiveAsync has no CancellationToken overload — closing the socket
                // is the only reliable way to unblock the pending receive on cancel.
                using (cancellationToken.Register(() => { try { _udp?.Close(); } catch { } }))
                {
                    UdpReceiveResult result = await _udp.ReceiveAsync().ConfigureAwait(false);
                    return new UdpAssociateDatagram(result.RemoteEndPoint, result.Buffer);
                }
#endif
            }

            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException("UDP source does not expose a stream — use SendAsync/ReceiveAsync.");

            protected override void Dispose(bool isDisposing)
            {
                try { _udp?.Close(); } catch { }
                _udp?.Dispose();
                _udp = null;
                base.Dispose(isDisposing);
            }
        }
    }
}
