using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.openssh.com/txt/socks4.protocol"/>
    /// </summary>
    internal class Socks4_Request
    {
        const long socks4aDomain = 0x00000001; //0.0.0.x with x non-zero
        private Socks4_Request()
        {

        }

        public static Socks4_Request CreateConnect(Uri uri, string? id = null)
        {
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

            var result = new Socks4_Request()
            {
                CMD = Socks4_CMD.Connect,
                DSTPORT = (UInt16)uri.Port,
                ID = id ?? string.Empty
            };
            switch (uri.HostNameType)
            {
                case UriHostNameType.IPv4:
                    result.DSTIP = IPAddress.Parse(uri.Host);
                    break;

                case UriHostNameType.Dns:
                    result.DOMAIN = uri.Host;
                    result.DSTIP = new IPAddress(socks4aDomain);
                    break;

                default:
                    throw new InvalidDataException($"Address input must be Ipv4 or Dns");
            }
            return result;
        }
        public static Socks4_Request CreateBind(string? id = null)
        {
            return new Socks4_Request()
            {
                CMD = Socks4_CMD.Bind,
                DSTIP = IPAddress.Any,
                DSTPORT = 0,
                ID = id ?? string.Empty
            };
        }


        public byte VER { get; private set; } = 0x04;
        public Socks4_CMD CMD { get; private set; }
        public UInt16 DSTPORT { get; private set; }
        public IPAddress DSTIP { get; private set; } = IPAddress.None;
        public string ID { get; private set; } = string.Empty;
        /// <summary>
        /// for socks4a
        /// </summary>
        public string DOMAIN { get; private set; } = string.Empty;
        public bool IsDomain
        {
            get
            {
                var address = DSTIP?.GetAddressBytes();
                return
                    address != null &&
                    address.Length == 4 &&
                    address[0] == 0 &&
                    address[1] == 0 &&
                    address[2] == 0 &&
                    address[3] != 0;
            }
        }

        internal static async Task<Socks4_Request> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks4_Request socks4_Request = new Socks4_Request();
            byte[] buffer = await stream.ReadBytesAsync(8, cancellationToken);
            socks4_Request.VER = buffer[0];
            socks4_Request.CMD = (Socks4_CMD)buffer[1];
            socks4_Request.DSTPORT = BitConverter.ToUInt16(buffer.Skip(2).Take(2).Reverse().ToArray(), 0);
            socks4_Request.DSTIP = new IPAddress(buffer.Skip(4).ToArray());
            buffer = await stream.ReadUntilNullTerminated(cancellationToken: cancellationToken);
            socks4_Request.ID = Encoding.ASCII.GetString(buffer);
            if (socks4_Request.IsDomain)
            {
                buffer = await stream.ReadUntilNullTerminated(cancellationToken: cancellationToken);
                socks4_Request.DOMAIN = Encoding.ASCII.GetString(buffer);
            }
            return socks4_Request;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return this.VER;
            yield return (byte)this.CMD;
            yield return (byte)this.DSTPORT;
            yield return (byte)(this.DSTPORT >> 8);
            foreach (byte b in this.DSTIP.GetAddressBytes())
            {
                yield return b;
            }
            if (!string.IsNullOrWhiteSpace(this.ID))
            {
                foreach (byte b in Encoding.ASCII.GetBytes(this.ID))
                {
                    yield return b;
                }
            }
            yield return 0;
            if (this.IsDomain)
            {
                if (!string.IsNullOrWhiteSpace(this.DOMAIN))
                {
                    foreach (byte b in Encoding.ASCII.GetBytes(this.DOMAIN))
                    {
                        yield return b;
                    }
                }
                yield return 0;
            }
        }
    }
    internal static class Socks4_Request_Extension
    {
        internal static Task<Socks4_Request> Read_Socks4_Request_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks4_Request.ReadAsync(stream, cancellationToken);
    }
}
