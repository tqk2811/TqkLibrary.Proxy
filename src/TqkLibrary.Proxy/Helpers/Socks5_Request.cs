using System.Net;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    public class Socks5_Request : IPacketData
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Socks5_Request()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public static Socks5_Request CreateConnect(Uri uri)
        {
            return new Socks5_Request()
            {
                CMD = Socks5_CMD.EstablishStreamConnection,
                DSTADDR = new Socks5_DSTADDR(uri),
                DSTPORT = (UInt16)uri.Port,
            };
        }
        public static Socks5_Request CreateBind()
        {
            return new Socks5_Request()
            {
                CMD = Socks5_CMD.EstablishPortBinding,
                DSTADDR = new Socks5_DSTADDR(IPAddress.Any),
                DSTPORT = 0,
            };
        }
        public static Socks5_Request CreateUdp()
        {
            return new Socks5_Request()
            {
                CMD = Socks5_CMD.AssociateUDP,
                DSTADDR = new Socks5_DSTADDR(IPAddress.Any),
                DSTPORT = 0,
            };
        }


        public byte VER { get; private set; } = 0x05;
        public Socks5_CMD CMD { get; private set; }
        public byte RSV { get; private set; }
        public Socks5_DSTADDR DSTADDR { get; private set; }
        public UInt16 DSTPORT { get; private set; }
        public Uri Uri
        {
            get
            {
                string scheme = DSTADDR.ATYP switch
                {
                    Socks5_ATYP.IpV6 => "tcp",
                    Socks5_ATYP.IpV4 => "tcp",
                    Socks5_ATYP.DomainName => "http",
                    _ => throw new NotSupportedException($"{DSTADDR.ATYP}"),
                };
                string host = DSTADDR.ATYP == Socks5_ATYP.DomainName ? DSTADDR.Domain : DSTADDR.IPAddress.ToString();
                return new Uri($"{scheme}://{host}:{DSTPORT}");
            }
        }

        public static async Task<Socks5_Request> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_Request socks5_Connection = new Socks5_Request();
            byte[] buffer = await stream.ReadBytesAsync(3, cancellationToken);
            socks5_Connection.VER = buffer[0];
            socks5_Connection.CMD = (Socks5_CMD)buffer[1];
            socks5_Connection.RSV = buffer[2];
            socks5_Connection.DSTADDR = await stream.Read_Socks5_DSTADDR_Async(cancellationToken);
            socks5_Connection.DSTPORT = (UInt16)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(await stream.ReadBytesAsync(2, cancellationToken), 0));
            return socks5_Connection;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)CMD;
            yield return (byte)RSV;
            foreach (byte b in DSTADDR!.GetBytes())
            {
                yield return b;
            }
            foreach (var b in BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)DSTPORT)))
            {
                yield return b;
            }
        }
    }
    public static class Socks5_Request_Extensions
    {
        public static Task<Socks5_Request> Read_Socks5_Request_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_Request.ReadAsync(stream, cancellationToken);
    }
}
