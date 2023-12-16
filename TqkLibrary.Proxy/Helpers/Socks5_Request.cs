using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    internal class Socks5_Request
    {
        internal Socks5_Request(Socks5_CMD socks5_CMD, Uri uri)
        {
            this.CMD = socks5_CMD;
            this.DSTADDR = new Socks5_DSTADDR(uri);
            this.DSTPORT = (UInt16)uri.Port;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Socks5_Request()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        internal byte VER { get; private set; } = 0x05;
        internal Socks5_CMD CMD { get; private set; }
        internal byte RSV { get; private set; }
        internal Socks5_DSTADDR DSTADDR { get; private set; }
        internal UInt16 DSTPORT { get; private set; }


        internal static async Task<Socks5_Request> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_Request socks5_Connection = new Socks5_Request();
            byte[] buffer = await stream.ReadBytesAsync(3, cancellationToken);
            socks5_Connection.VER = buffer[0];
            socks5_Connection.CMD = (Socks5_CMD)buffer[1];
            socks5_Connection.RSV = buffer[2];
            socks5_Connection.DSTADDR = await stream.Read_Socks5_DSTADDR_Async(cancellationToken);
            socks5_Connection.DSTPORT = BitConverter.ToUInt16((await stream.ReadBytesAsync(2, cancellationToken)).Reverse().ToArray(), 0);
            return socks5_Connection;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)CMD;
            yield return (byte)RSV;
            foreach (byte b in DSTADDR!.GetBytes())
            {
                yield return b;
            }
            yield return (byte)DSTPORT;
            yield return (byte)(DSTPORT >> 8);
        }
    }
    internal static class Socks5_Request_Extensions
    {
        internal static Task<Socks5_Request> Read_Socks5_Request_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_Request.ReadAsync(stream, cancellationToken);
    }
}
