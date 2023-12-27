using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    internal class Socks5_RequestResponse
    {
        internal Socks5_RequestResponse(Socks5_STATUS socks5_STATUS, Uri uri)
        {
            this.STATUS = socks5_STATUS;
            this.BNDADDR = new Socks5_DSTADDR(uri);
            this.BNDPORT = (UInt16)uri.Port;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Socks5_RequestResponse()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {

        }
        internal byte VER { get; private set; } = 0x05;
        internal Socks5_STATUS STATUS { get; private set; }
        internal byte RSV { get; private set; } = 0x00;
        internal Socks5_DSTADDR BNDADDR { get; private set; }
        internal UInt16 BNDPORT { get; private set; }
        internal IPEndPoint IPEndPoint { get { return new IPEndPoint(BNDADDR.IPAddress, BNDPORT); } }

        internal static async Task<Socks5_RequestResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_RequestResponse socks5_ConnectionResponse = new Socks5_RequestResponse();
            byte[] buffer = await stream.ReadBytesAsync(3, cancellationToken);
            socks5_ConnectionResponse.VER = buffer[0];
            socks5_ConnectionResponse.STATUS = (Socks5_STATUS)buffer[1];
            socks5_ConnectionResponse.RSV = buffer[2];
            socks5_ConnectionResponse.BNDADDR = await stream.Read_Socks5_DSTADDR_Async(cancellationToken);
            socks5_ConnectionResponse.BNDPORT = (UInt16)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(await stream.ReadBytesAsync(2, cancellationToken), 0));
            return socks5_ConnectionResponse;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)STATUS;
            yield return RSV;
            foreach (byte b in BNDADDR.GetBytes())
            {
                yield return b;
            }
            foreach (var b in BitConverter.GetBytes(IPAddress.HostToNetworkOrder(this.BNDPORT)))
            {
                yield return b;
            }
        }
    }
    internal static class Socks5_RequestResponse_Extensions
    {
        internal static Task<Socks5_RequestResponse> Read_Socks5_RequestResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_RequestResponse.ReadAsync(stream, cancellationToken);
    }
}
