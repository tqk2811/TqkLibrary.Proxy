namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A way out that can carry datagrams — SOCKS5 UDP ASSOCIATE, or a tunnel with an IP stack of
    /// its own.
    /// </summary>
    /// <remarks>
    /// Most cannot: an HTTP proxy has no datagram in its protocol at all, and neither does SSH -W.
    /// Those used to be made to answer anyway, because <see cref="IProxySource"/> asked everyone, so
    /// every one of them carried a <c>false</c> and a method that threw. Not implementing this says
    /// the same thing once, and says it to the compiler.
    ///
    /// The flag stays because "can" has two halves. A protocol either has datagrams or it does not,
    /// and that is this interface; an upstream that speaks the protocol may still refuse — a SOCKS5
    /// server built without UDP, a wireproxy whose SOCKS5 is TCP-only — and that is
    /// <see cref="IsSupportUdp"/>. A caller must pass both before asking for a tunnel.
    /// </remarks>
    public interface IUdpCapable
    {
        /// <summary>
        /// False when this particular instance is pointed at an upstream that will not do it. Ask
        /// before <see cref="GetUdpAssociateSourceAsync"/>: a way out that says no here throws.
        /// </summary>
        bool IsSupportUdp { get; }

        Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default);
    }
}
