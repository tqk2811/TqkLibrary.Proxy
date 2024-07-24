using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    public class Socks5_DSTADDR : IPacketData
    {
        public Socks5_DSTADDR(Uri uri)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));

            switch (uri.HostNameType)
            {
                case UriHostNameType.Dns:
                    if (uri.Host.Length > 255) throw new InvalidDataException($"Host name too long: {uri.Host}");
                    Domain = uri.Host;
                    ATYP = Socks5_ATYP.DomainName;
                    break;

                case UriHostNameType.IPv4:
                    IPAddress = IPAddress.Parse(uri.Host);
                    ATYP = Socks5_ATYP.IpV4;
                    break;

                case UriHostNameType.IPv6:
                    IPAddress = IPAddress.Parse(uri.Host);
                    ATYP = Socks5_ATYP.IpV6;
                    break;

                default:
                    throw new InvalidDataException($"Invalid type uri input: {uri.HostNameType}");
            }
        }
        public Socks5_DSTADDR(IPAddress iPAddress)
        {
            if (iPAddress is null)
                throw new ArgumentNullException(nameof(iPAddress));

            switch (iPAddress.AddressFamily)
            {
                case AddressFamily.InterNetwork:
                    IPAddress = iPAddress;
                    ATYP = Socks5_ATYP.IpV4;
                    break;

                case AddressFamily.InterNetworkV6:
                    IPAddress = iPAddress;
                    ATYP = Socks5_ATYP.IpV6;
                    break;

                default:
                    throw new NotSupportedException(iPAddress.AddressFamily.ToString());
            }
        }

        private Socks5_DSTADDR()
        {

        }

        public string Domain { get; private set; } = string.Empty;
        public IPAddress IPAddress { get; private set; } = IPAddress.None;
        public Socks5_ATYP ATYP { get; private set; }

        public static async Task<Socks5_DSTADDR> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_DSTADDR socks5_DSTADDR = new Socks5_DSTADDR();
            socks5_DSTADDR.ATYP = (Socks5_ATYP)await stream.ReadByteAsync(cancellationToken);
            switch (socks5_DSTADDR.ATYP)
            {
                case Socks5_ATYP.IpV4:
                case Socks5_ATYP.IpV6:
                    {
                        byte[] buffer = await stream.ReadBytesAsync(
                            socks5_DSTADDR.ATYP == Socks5_ATYP.IpV4 ? 4 : 16,
                            cancellationToken);
                        socks5_DSTADDR.IPAddress = new IPAddress(buffer);
                        break;
                    }

                case Socks5_ATYP.DomainName:
                    {
                        byte b = await stream.ReadByteAsync(cancellationToken);
                        byte[] buffer = await stream.ReadBytesAsync(b, cancellationToken);
                        socks5_DSTADDR.Domain = Encoding.ASCII.GetString(buffer);
                        break;
                    }

                default:
                    throw new InvalidDataException();
            }

            return socks5_DSTADDR;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return (byte)ATYP;
            if (ATYP == Socks5_ATYP.DomainName)
            {
                byte[] domainBuffer = Encoding.ASCII.GetBytes(Domain);
                yield return (byte)domainBuffer.Length;
                foreach (byte b in domainBuffer)
                {
                    yield return b;
                }
            }
            else
            {
                foreach (byte b in IPAddress.GetAddressBytes())
                {
                    yield return b;
                }
            }
        }
    }
    public static class Socks5_DSTADDR_Extensions
    {
        public static Task<Socks5_DSTADDR> Read_Socks5_DSTADDR_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_DSTADDR.ReadAsync(stream, cancellationToken);
    }
}
