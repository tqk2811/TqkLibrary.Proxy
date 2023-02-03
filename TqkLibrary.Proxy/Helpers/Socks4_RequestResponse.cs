using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.openssh.com/txt/socks4.protocol"/>
    /// </summary>
    internal class Socks4_RequestResponse
    {
        public Socks4_RequestResponse(Socks4_REP socks4_REP, Uri uri)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));
            if (uri.HostNameType != UriHostNameType.IPv4) throw new InvalidDataException($"Address input must be Ipv4");

            this.REP = socks4_REP;
            this.DSTIP = IPAddress.Parse(uri.Host);
            this.DSTPORT = (UInt16)uri.Port;
        }
        public Socks4_RequestResponse(Socks4_REP socks4_REP, IPEndPoint iPEndPoint)
        {
            if (iPEndPoint is null) throw new ArgumentNullException(nameof(iPEndPoint));
            if (iPEndPoint.AddressFamily != AddressFamily.InterNetwork) throw new InvalidDataException($"Address input must be Ipv4");

            this.REP = socks4_REP;
            this.DSTIP = iPEndPoint.Address;
            this.DSTPORT = (UInt16)iPEndPoint.Port;
        }
        public Socks4_RequestResponse(Socks4_REP socks4_REP, IPAddress dstIp, UInt16 dstPort)
        {
            if (dstIp is null) throw new ArgumentNullException(nameof(dstIp));
            if (dstIp.AddressFamily != AddressFamily.InterNetwork) throw new InvalidDataException($"Address input must be Ipv4");

            this.REP = socks4_REP;
            this.DSTIP = dstIp;
            this.DSTPORT = dstPort;
        }
        private Socks4_RequestResponse()
        {

        }


        public byte VN { get; private set; }
        public Socks4_REP REP { get; private set; }
        public UInt16 DSTPORT { get; private set; }
        public IPAddress DSTIP { get; private set; }
        public IPEndPoint IPEndPoint { get { return new IPEndPoint(DSTIP, DSTPORT); } }

        internal static async Task<Socks4_RequestResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks4_RequestResponse socks4_RequestResponse = new Socks4_RequestResponse();
            byte[] buffer = await stream.ReadBytesAsync(8, cancellationToken);
            socks4_RequestResponse.VN = buffer[0];
            socks4_RequestResponse.REP = (Socks4_REP)buffer[1];
            socks4_RequestResponse.DSTPORT = BitConverter.ToUInt16(buffer, 2);
            socks4_RequestResponse.DSTIP = new IPAddress(buffer.Skip(4).ToArray());
            return socks4_RequestResponse;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return this.VN;
            yield return (byte)this.REP;
            yield return (byte)(this.DSTPORT >> 8);
            yield return (byte)this.DSTPORT;
            foreach (byte b in this.DSTIP.GetAddressBytes())
            {
                yield return b;
            }
        }
    }
    internal static class Socks4_RequestResponse_Extension
    {
        internal static Task<Socks4_RequestResponse> Read_Socks4_RequestResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks4_RequestResponse.ReadAsync(stream, cancellationToken);
    }
}
