using System.Net;

namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// SOCKS5 UDP ASSOCIATE client tunnel. Lifetime is bounded by the TCP control connection
    /// established during <see cref="AssociateAsync"/>; the server tears the UDP relay down
    /// when that TCP connection closes (RFC 1928 §6).
    /// </summary>
    public interface IUdpAssociateSource : IBaseSource
    {
        /// <summary>
        /// Endpoint the SOCKS5 server returned as BND.ADDR:BND.PORT — the UDP relay address
        /// the client should send datagrams to. Null until <see cref="AssociateAsync"/> succeeds.
        /// </summary>
        IPEndPoint? RelayEndPoint { get; }

        /// <summary>
        /// Local endpoint the client UDP socket is bound to (after AssociateAsync).
        /// </summary>
        IPEndPoint? LocalEndPoint { get; }

        /// <summary>
        /// Perform the SOCKS5 UDP ASSOCIATE handshake on the TCP control channel and open the
        /// local UDP socket. Returns the relay endpoint to send datagrams to. The DST.ADDR/PORT
        /// in the ASSOCIATE request is sent as 0.0.0.0:0 (RFC 1928 §6 "unknown" client form),
        /// which all standards-conformant servers accept.
        /// </summary>
        Task<IPEndPoint> AssociateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Encode (RSV/FRAG/ATYP/DST.ADDR/DST.PORT/DATA) per RFC 1928 §7 and send to the relay.
        /// </summary>
        Task SendAsync(IPEndPoint destination, byte[] payload, int offset, int count, CancellationToken cancellationToken = default);

        /// <summary>
        /// Receive one datagram from the relay; the returned <see cref="UdpAssociateDatagram.Source"/>
        /// is the real remote peer (decoded from the SOCKS5 header).
        /// </summary>
        Task<UdpAssociateDatagram> ReceiveAsync(CancellationToken cancellationToken = default);
    }

    public readonly struct UdpAssociateDatagram
    {
        public UdpAssociateDatagram(IPEndPoint source, byte[] payload)
        {
            Source = source;
            Payload = payload;
        }

        /// <summary>Remote peer the relay received this datagram from.</summary>
        public IPEndPoint Source { get; }

        /// <summary>Datagram payload (already stripped of the SOCKS5 UDP header).</summary>
        public byte[] Payload { get; }
    }
}
