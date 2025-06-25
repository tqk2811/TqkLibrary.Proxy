namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxySource
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
