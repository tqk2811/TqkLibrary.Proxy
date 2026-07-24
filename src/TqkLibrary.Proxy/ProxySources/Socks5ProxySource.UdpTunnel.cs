using System.Net;
using System.Net.Sockets;
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
        // SOCKS5 UDP ASSOCIATE client (RFC 1928 §4 cmd 0x03, §7 datagram framing).
        //
        // Lifecycle:
        //   1. TCP control connection is opened and authenticated (BaseTunnel.ConnectAndAuthAsync).
        //   2. A UDP ASSOCIATE request is sent; the server replies with BND.ADDR:BND.PORT — the
        //      relay endpoint to which we send subsequent UDP datagrams.
        //   3. The TCP control connection MUST stay open for the duration of the UDP session; the
        //      server tears down the relay when it closes (RFC 1928 §6).
        //
        // Datagram framing (sent over UDP to BND.ADDR:BND.PORT):
        //   +----+------+------+----------+----------+----------+
        //   |RSV | FRAG | ATYP | DST.ADDR | DST.PORT |   DATA   |
        //   +----+------+------+----------+----------+----------+
        //   | 2  |  1   |  1   | Variable |    2     | Variable |
        //   RSV=0x0000, FRAG=0x00 (fragmentation not implemented — almost no real server uses it).
        public partial class UdpTunnel : BaseTunnel, IUdpAssociateSource
        {
            private UdpClient? _udp;

            public IPEndPoint? RelayEndPoint { get; private set; }
            public IPEndPoint? LocalEndPoint => _udp?.Client?.LocalEndPoint as IPEndPoint;

            internal protected UdpTunnel(Socks5ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {
            }

            public virtual async Task<IPEndPoint> AssociateAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();

                using var scope = _logger?.BeginScope("TunnelId:{TunnelId}", _tunnelId);

                await base.ConnectAndAuthAsync(cancellationToken);

                Socks5_Request request = Socks5_Request.CreateUdp();
                _logger?.LogInformation("UDP ASSOCIATE request");
                await _stream!.WriteAsync(request.GetByteArray(), cancellationToken);
                await _stream!.FlushAsync(cancellationToken);

                Socks5_RequestResponse response = await _stream!.Read_Socks5_RequestResponse_Async(cancellationToken);
                if (response.STATUS != Socks5_STATUS.RequestGranted)
                {
                    _logger?.LogWarning("UDP ASSOCIATE REJECTED status={Status}", response.STATUS);
                    throw new InitConnectSourceFailedException($"UDP ASSOCIATE failed: {response.STATUS}");
                }

                IPAddress relayAddr = await ResolveRelayAddressAsync(response.BNDADDR, cancellationToken);
                // Many SOCKS5 servers reply with 0.0.0.0 to mean "same host as TCP control" (RFC ambiguity).
                if (IPAddress.Any.Equals(relayAddr) || IPAddress.IPv6Any.Equals(relayAddr))
                {
                    relayAddr = (_tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address
                        ?? throw new InvalidOperationException("Cannot resolve proxy server address for UDP relay fallback");
                }

                RelayEndPoint = new IPEndPoint(relayAddr, response.BNDPORT);

                AddressFamily family = relayAddr.AddressFamily;
                _udp = new UdpClient(new IPEndPoint(family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
                // Connect "binds" the UDP socket's remote — only datagrams from the relay are accepted,
                // and we don't have to specify the endpoint on every SendAsync.
                _udp.Connect(RelayEndPoint);
                _logger?.LogInformation("UDP ASSOCIATE OK relay={Relay} local={Local}", RelayEndPoint, _udp.Client.LocalEndPoint);

                return RelayEndPoint;
            }

            public virtual async Task SendAsync(IPEndPoint destination, byte[] payload, int offset, int count, CancellationToken cancellationToken = default)
            {
                if (destination is null) throw new ArgumentNullException(nameof(destination));
                if (payload is null) throw new ArgumentNullException(nameof(payload));
                CheckIsDisposed();
                if (_udp is null || RelayEndPoint is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(UdpTunnel)}.{nameof(AssociateAsync)} first");

                byte[] datagram = Socks5_UdpDatagram.Encode(destination, payload, offset, count);
#if NET6_0_OR_GREATER
                // UdpClient is "connected" (see AssociateAsync) — this overload requires that.
                await _udp.SendAsync(datagram, cancellationToken).ConfigureAwait(false);
#else
                await _udp.SendAsync(datagram, datagram.Length).ConfigureAwait(false);
#endif
            }

            public virtual async Task<UdpAssociateDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                if (_udp is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(UdpTunnel)}.{nameof(AssociateAsync)} first");

#if NET6_0_OR_GREATER
                UdpReceiveResult result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                return ToAssociateDatagram(Socks5_UdpDatagram.Parse(result.Buffer));
#else
                // netstandard2.0: ReceiveAsync has no CancellationToken overload — closing the socket
                // is the only reliable way to unblock the pending receive on cancel.
                using (cancellationToken.Register(() => { try { _udp?.Close(); } catch { } }))
                {
                    UdpReceiveResult result = await _udp.ReceiveAsync().ConfigureAwait(false);
                    return ToAssociateDatagram(Socks5_UdpDatagram.Parse(result.Buffer));
                }
#endif
            }

            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException("UDP associate source does not expose a stream — use SendAsync/ReceiveAsync.");

            // Resolve BND.ADDR to an IPAddress. SOCKS5 servers almost always reply with an IP literal,
            // but ATYP=DomainName is technically allowed — in that case we DNS-resolve and prefer the
            // family that matches the TCP control peer (most relay deployments are single-family).
            private async Task<IPAddress> ResolveRelayAddressAsync(Socks5_DSTADDR bndaddr, CancellationToken cancellationToken)
            {
                if (bndaddr.ATYP != Socks5_ATYP.DomainName)
                    return bndaddr.IPAddress;

                IPAddress[] ips;
                try
                {
#if NET6_0_OR_GREATER
                    ips = await Dns.GetHostAddressesAsync(bndaddr.Domain, cancellationToken).ConfigureAwait(false);
#else
                    ips = await Dns.GetHostAddressesAsync(bndaddr.Domain).ConfigureAwait(false);
#endif
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to resolve SOCKS5 UDP relay domain '{Domain}', falling back to TCP control peer", bndaddr.Domain);
                    return (_tcpClient.Client.RemoteEndPoint as IPEndPoint)?.Address
                        ?? throw new InvalidOperationException($"Cannot resolve SOCKS5 UDP relay domain '{bndaddr.Domain}' and no TCP peer fallback available");
                }

                if (ips.Length == 0)
                    throw new InvalidOperationException($"DNS returned no addresses for SOCKS5 UDP relay domain '{bndaddr.Domain}'");

                AddressFamily preferred = (_tcpClient.Client.RemoteEndPoint as IPEndPoint)?.AddressFamily ?? AddressFamily.InterNetwork;
                return ips.FirstOrDefault(ip => ip.AddressFamily == preferred) ?? ips[0];
            }

            protected override void Dispose(bool isDisposing)
            {
                try { _udp?.Close(); } catch { }
                _udp?.Dispose();
                base.Dispose(isDisposing);
            }

            // Bridge between the rich Socks5_UdpDatagram (which can carry ATYP=DomainName) and the
            // IPEndPoint-only UdpAssociateDatagram surface. ATYP=DomainName in replies is rare —
            // surface as IPAddress.None so callers still see port/payload.
            private static UdpAssociateDatagram ToAssociateDatagram(Socks5_UdpDatagram parsed)
            {
                IPAddress addr = parsed.IPAddress ?? IPAddress.None;
                return new UdpAssociateDatagram(new IPEndPoint(addr, parsed.Port), parsed.Payload);
            }
        }
    }
}
