using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Enums;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// SOCKS5 UDP datagram framing per RFC 1928 §7:
    /// <code>
    /// +----+------+------+----------+----------+----------+
    /// |RSV | FRAG | ATYP | DST.ADDR | DST.PORT |   DATA   |
    /// +----+------+------+----------+----------+----------+
    /// | 2  |  1   |  1   | Variable |    2     | Variable |
    /// </code>
    /// RSV=0x0000, FRAG=0x00. Fragmentation (FRAG≠0) is not supported here — virtually no real
    /// SOCKS5 implementation uses it.
    /// </summary>
    public sealed class Socks5_UdpDatagram
    {
        public Socks5_ATYP ATYP { get; private set; }

        /// <summary>Set when ATYP is IPv4 or IPv6; null when ATYP=DomainName.</summary>
        public IPAddress? IPAddress { get; private set; }

        /// <summary>Set when ATYP=DomainName; null otherwise.</summary>
        public string? Domain { get; private set; }

        public ushort Port { get; private set; }

        public byte[] Payload { get; private set; } = Array.Empty<byte>();

        private Socks5_UdpDatagram() { }

        /// <summary>
        /// Encode an IPv4/IPv6 endpoint + payload into a SOCKS5 UDP datagram. Domain encoding is
        /// not exposed here (clients on this side never address by name on the wire).
        /// </summary>
        public static byte[] Encode(IPEndPoint endpoint, byte[] payload, int offset, int count)
        {
            if (endpoint is null) throw new ArgumentNullException(nameof(endpoint));
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (offset < 0 || count < 0 || offset + count > payload.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            Socks5_DSTADDR dstAddr = new Socks5_DSTADDR(endpoint.Address);
            byte[] addrBytes = dstAddr.GetByteArray(); // [ATYP][addr bytes]

            byte[] datagram = new byte[3 + addrBytes.Length + 2 + count];
            // datagram[0..2] = RSV(2)+FRAG(1) — all zero by allocation.
            Buffer.BlockCopy(addrBytes, 0, datagram, 3, addrBytes.Length);
            int portOffset = 3 + addrBytes.Length;
            datagram[portOffset] = (byte)((endpoint.Port >> 8) & 0xFF);
            datagram[portOffset + 1] = (byte)(endpoint.Port & 0xFF);
            Buffer.BlockCopy(payload, offset, datagram, portOffset + 2, count);
            return datagram;
        }

        /// <summary>
        /// Parse a SOCKS5 UDP datagram. Throws <see cref="InvalidDataException"/> if the buffer is
        /// truncated, and <see cref="NotSupportedException"/> if FRAG≠0 or ATYP is unknown.
        /// </summary>
        public static Socks5_UdpDatagram Parse(byte[] buffer)
        {
            if (buffer is null) throw new ArgumentNullException(nameof(buffer));
            // Smallest possible header: RSV(2)+FRAG(1)+ATYP(1).
            if (buffer.Length < 4)
                throw new InvalidDataException($"SOCKS5 UDP datagram too short ({buffer.Length} bytes)");
            if (buffer[2] != 0)
                throw new NotSupportedException($"Fragmented SOCKS5 UDP datagrams are not supported (FRAG=0x{buffer[2]:X2})");

            Socks5_UdpDatagram dgram = new Socks5_UdpDatagram();
            dgram.ATYP = (Socks5_ATYP)buffer[3];
            int cursor = 4;

            switch (dgram.ATYP)
            {
                case Socks5_ATYP.IpV4:
                    if (cursor + 4 + 2 > buffer.Length)
                        throw new InvalidDataException("SOCKS5 UDP datagram truncated in IPv4 DST.ADDR/DST.PORT");
                    dgram.IPAddress = new IPAddress(new[] { buffer[cursor], buffer[cursor + 1], buffer[cursor + 2], buffer[cursor + 3] });
                    cursor += 4;
                    break;

                case Socks5_ATYP.IpV6:
                    {
                        if (cursor + 16 + 2 > buffer.Length)
                            throw new InvalidDataException("SOCKS5 UDP datagram truncated in IPv6 DST.ADDR/DST.PORT");
                        byte[] ip6 = new byte[16];
                        Buffer.BlockCopy(buffer, cursor, ip6, 0, 16);
                        dgram.IPAddress = new IPAddress(ip6);
                        cursor += 16;
                        break;
                    }

                case Socks5_ATYP.DomainName:
                    {
                        if (cursor + 1 > buffer.Length)
                            throw new InvalidDataException("SOCKS5 UDP datagram truncated before domain length");
                        int domainLen = buffer[cursor];
                        if (cursor + 1 + domainLen + 2 > buffer.Length)
                            throw new InvalidDataException("SOCKS5 UDP datagram truncated in domain DST.ADDR/DST.PORT");
                        dgram.Domain = Encoding.ASCII.GetString(buffer, cursor + 1, domainLen);
                        cursor += 1 + domainLen;
                        break;
                    }

                default:
                    throw new NotSupportedException($"Unknown SOCKS5 ATYP in UDP datagram: 0x{(byte)dgram.ATYP:X2}");
            }

            dgram.Port = (ushort)((buffer[cursor] << 8) | buffer[cursor + 1]);
            cursor += 2;

            int payloadLen = buffer.Length - cursor;
            dgram.Payload = new byte[payloadLen];
            Buffer.BlockCopy(buffer, cursor, dgram.Payload, 0, payloadLen);

            return dgram;
        }
    }
}
