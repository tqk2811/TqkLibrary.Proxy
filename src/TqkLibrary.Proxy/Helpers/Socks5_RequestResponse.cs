using System.Net;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    public class Socks5_RequestResponse : IPacketData
    {
        public Socks5_RequestResponse(Socks5_STATUS socks5_STATUS, IPEndPoint iPEndPoint)
        {
            if (iPEndPoint is null)
                throw new ArgumentNullException(nameof(iPEndPoint));
            STATUS = socks5_STATUS;
            BNDADDR = new Socks5_DSTADDR(iPEndPoint.Address);
            BNDPORT = (UInt16)iPEndPoint.Port;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Socks5_RequestResponse()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {

        }
        public byte VER { get; private set; } = 0x05;
        public Socks5_STATUS STATUS { get; private set; }
        public byte RSV { get; private set; } = 0x00;
        public Socks5_DSTADDR BNDADDR { get; private set; }
        public UInt16 BNDPORT { get; private set; }
        public IPEndPoint IPEndPoint { get { return new IPEndPoint(BNDADDR.IPAddress, BNDPORT); } }

        public static async Task<Socks5_RequestResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_RequestResponse socks5_ConnectionResponse = new Socks5_RequestResponse();
            byte[] buffer = await stream.ReadBytesAsync(3, cancellationToken);
            socks5_ConnectionResponse.VER = buffer[0];
            socks5_ConnectionResponse.STATUS = (Socks5_STATUS)buffer[1];
            socks5_ConnectionResponse.RSV = buffer[2];
            socks5_ConnectionResponse.BNDADDR = await stream.Read_Socks5_DSTADDR_Async(cancellationToken);
            socks5_ConnectionResponse.BNDPORT = (UInt16)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(await stream.ReadBytesAsync(2, cancellationToken), 0));
            return socks5_ConnectionResponse;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)STATUS;
            yield return RSV;
            foreach (byte b in BNDADDR.GetBytes())
            {
                yield return b;
            }
            foreach (var b in BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)BNDPORT)))
            {
                yield return b;
            }
        }
    }
    public static class Socks5_RequestResponse_Extensions
    {
        public static Task<Socks5_RequestResponse> Read_Socks5_RequestResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_RequestResponse.ReadAsync(stream, cancellationToken);
    }
}
