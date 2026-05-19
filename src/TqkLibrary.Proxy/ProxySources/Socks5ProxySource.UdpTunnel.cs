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
                await base.ConnectAndAuthAsync(cancellationToken);

                Socks5_Request request = Socks5_Request.CreateUdp();
                _logger?.LogInformation($"{_tunnelId} UDP ASSOCIATE request");
                await _stream!.WriteAsync(request.GetByteArray(), cancellationToken);
                await _stream!.FlushAsync(cancellationToken);

                Socks5_RequestResponse response = await _stream!.Read_Socks5_RequestResponse_Async(cancellationToken);
                if (response.STATUS != Socks5_STATUS.RequestGranted)
                {
                    _logger?.LogWarning($"{_tunnelId} UDP ASSOCIATE REJECTED status={response.STATUS}");
                    throw new InitConnectSourceFailedException($"UDP ASSOCIATE failed: {response.STATUS}");
                }

                IPAddress relayAddr = response.BNDADDR.IPAddress;
                // Many SOCKS5 servers reply with 0.0.0.0 to mean "same host as TCP control" (RFC ambiguity).
                if (IPAddress.Any.Equals(relayAddr) || IPAddress.IPv6Any.Equals(relayAddr))
                    relayAddr = _proxySource.IPEndPoint.Address;

                RelayEndPoint = new IPEndPoint(relayAddr, response.BNDPORT);

                AddressFamily family = relayAddr.AddressFamily;
                _udp = new UdpClient(new IPEndPoint(family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0));
                // Connect "binds" the UDP socket's remote — only datagrams from the relay are accepted,
                // and we don't have to specify the endpoint on every SendAsync.
                _udp.Connect(RelayEndPoint);
                _logger?.LogInformation($"{_tunnelId} UDP ASSOCIATE OK relay={RelayEndPoint} local={_udp.Client.LocalEndPoint}");

                return RelayEndPoint;
            }

            public virtual async Task SendAsync(IPEndPoint destination, byte[] payload, int offset, int count, CancellationToken cancellationToken = default)
            {
                if (destination is null) throw new ArgumentNullException(nameof(destination));
                if (payload is null) throw new ArgumentNullException(nameof(payload));
                CheckIsDisposed();
                if (_udp is null || RelayEndPoint is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(UdpTunnel)}.{nameof(AssociateAsync)} first");

                byte[] datagram = BuildDatagram(destination, payload, offset, count);
                await _udp.SendAsync(datagram, datagram.Length).ConfigureAwait(false);
            }

            public virtual async Task<UdpAssociateDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                if (_udp is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(UdpTunnel)}.{nameof(AssociateAsync)} first");

#if NET6_0_OR_GREATER
                UdpReceiveResult result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                return ParseDatagram(result.Buffer);
#else
                // netstandard2.0: ReceiveAsync has no CancellationToken overload — closing the socket
                // is the only reliable way to unblock the pending receive on cancel.
                using (cancellationToken.Register(() => { try { _udp?.Close(); } catch { } }))
                {
                    UdpReceiveResult result = await _udp.ReceiveAsync().ConfigureAwait(false);
                    return ParseDatagram(result.Buffer);
                }
#endif
            }

            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
                => throw new NotSupportedException("UDP associate source does not expose a stream — use SendAsync/ReceiveAsync.");

            protected override void Dispose(bool isDisposing)
            {
                try { _udp?.Close(); } catch { }
                _udp?.Dispose();
                base.Dispose(isDisposing);
            }

            // RSV(2)=0 FRAG(1)=0 ATYP(1) DST.ADDR(var) DST.PORT(2) DATA
            private static byte[] BuildDatagram(IPEndPoint destination, byte[] payload, int offset, int count)
            {
                Socks5_DSTADDR dstAddr = new Socks5_DSTADDR(destination.Address);
                byte[] addrBytes = dstAddr.GetByteArray();
                byte[] datagram = new byte[3 + addrBytes.Length + 2 + count];
                // RSV (already 0), FRAG (already 0)
                Buffer.BlockCopy(addrBytes, 0, datagram, 3, addrBytes.Length);
                int portOffset = 3 + addrBytes.Length;
                datagram[portOffset] = (byte)((destination.Port >> 8) & 0xFF);
                datagram[portOffset + 1] = (byte)(destination.Port & 0xFF);
                Buffer.BlockCopy(payload, offset, datagram, portOffset + 2, count);
                return datagram;
            }

            private static UdpAssociateDatagram ParseDatagram(byte[] buffer)
            {
                if (buffer.Length < 10) // min header: RSV(2)+FRAG(1)+ATYP(1)+IPv4(4)+PORT(2)
                    throw new InvalidDataException($"SOCKS5 UDP datagram too short ({buffer.Length} bytes)");
                if (buffer[2] != 0)
                    throw new NotSupportedException($"Fragmented SOCKS5 UDP datagrams are not supported (FRAG=0x{buffer[2]:X2})");

                int atyp = buffer[3];
                int cursor = 4;
                IPAddress source;
                switch (atyp)
                {
                    case 0x01: // IPv4
                        source = new IPAddress(new[] { buffer[cursor], buffer[cursor + 1], buffer[cursor + 2], buffer[cursor + 3] });
                        cursor += 4;
                        break;
                    case 0x04: // IPv6
                        {
                            byte[] ip6 = new byte[16];
                            Buffer.BlockCopy(buffer, cursor, ip6, 0, 16);
                            source = new IPAddress(ip6);
                            cursor += 16;
                            break;
                        }
                    case 0x03: // Domain — uncommon in replies. Surface as 0.0.0.0 so callers can still see the port/payload.
                        {
                            int domainLen = buffer[cursor];
                            cursor += 1 + domainLen;
                            source = IPAddress.None;
                            break;
                        }
                    default:
                        throw new NotSupportedException($"Unknown SOCKS5 ATYP in UDP reply: 0x{atyp:X2}");
                }

                if (cursor + 2 > buffer.Length)
                    throw new InvalidDataException("SOCKS5 UDP datagram truncated before DST.PORT");
                int port = (buffer[cursor] << 8) | buffer[cursor + 1];
                cursor += 2;

                int payloadLen = buffer.Length - cursor;
                byte[] payload = new byte[payloadLen];
                Buffer.BlockCopy(buffer, cursor, payload, 0, payloadLen);

                return new UdpAssociateDatagram(new IPEndPoint(source, port), payload);
            }
        }
    }
}
