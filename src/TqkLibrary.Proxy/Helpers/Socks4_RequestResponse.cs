using System.Net;
using System.Net.Sockets;
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

            REP = socks4_REP;
            DSTIP = IPAddress.Parse(uri.Host);
            DSTPORT = (UInt16)uri.Port;
        }
        public Socks4_RequestResponse(Socks4_REP socks4_REP, IPEndPoint iPEndPoint)
        {
            if (iPEndPoint is null) throw new ArgumentNullException(nameof(iPEndPoint));
            if (iPEndPoint.AddressFamily != AddressFamily.InterNetwork) throw new InvalidDataException($"Address input must be Ipv4");

            REP = socks4_REP;
            DSTIP = iPEndPoint.Address;
            DSTPORT = (UInt16)iPEndPoint.Port;
        }
        public Socks4_RequestResponse(Socks4_REP socks4_REP, IPAddress dstIp, UInt16 dstPort)
        {
            if (dstIp is null) throw new ArgumentNullException(nameof(dstIp));
            if (dstIp.AddressFamily != AddressFamily.InterNetwork) throw new InvalidDataException($"Address input must be Ipv4");

            REP = socks4_REP;
            DSTIP = dstIp;
            DSTPORT = dstPort;
        }
        private Socks4_RequestResponse()
        {

        }


        public byte VN { get; private set; }
        public Socks4_REP REP { get; private set; }
        /// <summary>
        /// destination port, meaningful if granted in BIND, otherwise ignore
        /// </summary>
        public UInt16 DSTPORT { get; private set; }
        /// <summary>
        /// destination IP, as above – the ip:port the client should bind to
        /// </summary>
        public IPAddress DSTIP { get; private set; } = IPAddress.None;
        public IPEndPoint IPEndPoint { get { return new IPEndPoint(DSTIP, DSTPORT); } }

        internal static async Task<Socks4_RequestResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks4_RequestResponse socks4_RequestResponse = new Socks4_RequestResponse();
            byte[] buffer = await stream.ReadBytesAsync(8, cancellationToken);
            socks4_RequestResponse.VN = buffer[0];
            socks4_RequestResponse.REP = (Socks4_REP)buffer[1];
            socks4_RequestResponse.DSTPORT = (UInt16)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(buffer, 2));
            socks4_RequestResponse.DSTIP = new IPAddress(buffer.Skip(4).ToArray());
            return socks4_RequestResponse;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return VN;
            yield return (byte)REP;
            foreach (var b in BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)DSTPORT)))
            {
                yield return b;
            }
            foreach (byte b in DSTIP.GetAddressBytes())
            {
                yield return b;
            }
        }

        public override string ToString()
        {
            return $"{REP}";
        }
    }
    internal static class Socks4_RequestResponse_Extension
    {
        internal static Task<Socks4_RequestResponse> Read_Socks4_RequestResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks4_RequestResponse.ReadAsync(stream, cancellationToken);
    }
}
