namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A way out that can listen for an incoming connection on the far side — SOCKS4 and SOCKS5
    /// BIND, which is what an FTP client in active mode needs.
    /// </summary>
    /// <remarks>
    /// Almost nothing can. A tunnel is given an address that is private to it and not reachable
    /// from the internet, and an HTTP proxy has no BIND in its protocol; of everything here only the
    /// machine's own stack and the two SOCKS clients offer it. See <see cref="IUdpCapable"/> for why
    /// the flag sits beside the method rather than replacing it.
    /// </remarks>
    public interface IBindCapable
    {
        /// <summary>
        /// False when this particular instance is pointed at an upstream that will not do it. Ask
        /// before <see cref="GetBindSourceAsync"/>: a way out that says no here throws.
        /// </summary>
        bool IsSupportBind { get; }

        Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default);
    }
}
