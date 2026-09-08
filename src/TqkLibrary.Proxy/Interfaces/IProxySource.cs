namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A way out: a factory of tunnels, not a tunnel. One instance is meant to be shared by every
    /// connection that leaves the same way, because an implementation may hold a session behind it
    /// (an SSH channel, a WireGuard handshake, a subprocess) that must not be rebuilt per request.
    /// </summary>
    /// <remarks>
    /// Ownership: whoever built the instance releases it, and nobody else. A server handed a source
    /// by <see cref="IProxyServerHandler.GetProxySourceAsync"/> is a borrower — it must not dispose
    /// what it did not create, or the next connection through the same way out finds it gone.
    ///
    /// Disposal is async because that is what the stateful implementations actually need: closing a
    /// session means a network round trip, and waiting for a subprocess to exit means waiting.
    /// Sources with nothing to release return a completed task. It sits on the interface rather than
    /// on the implementations that happen to need it so that the owner has a single call that always
    /// does the right thing: the earlier arrangement — the caller testing for
    /// <see cref="IDisposable"/> — silently leaked any source that only offered the async form, and
    /// nothing about that showed up at compile time.
    /// </remarks>
    public interface IProxySource : IAsyncDisposable
    {
        /// <summary>
        /// for socks5
        /// </summary>
        bool IsSupportUdp { get; }

        /// <summary>
        /// for socks5, dns 
        /// </summary>
        bool IsSupportIpv6 { get; }

        /// <summary>
        /// For socks4 and socks5
        /// </summary>
        bool IsSupportBind { get; }

        /// <summary>
        /// 
        /// </summary>
        Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default);
    }
}
