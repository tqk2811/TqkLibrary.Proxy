namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxySource
    {
        /// <summary>
        /// for socks5
        /// </summary>
        bool IsSupportUdp { get; }

        /// <summary>
        /// for socks5
        /// </summary>
        bool IsSupportIpv6 { get; }

        /// <summary>
        /// For socks4 and socks5
        /// </summary>
        bool IsSupportBind { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<IConnectSource> InitConnectAsync(Uri address, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"><see cref="Uri.HostNameType"/> must be <see cref="UriHostNameType.IPv4"/> or <see cref="UriHostNameType.IPv6"/></param>
        /// <returns></returns>
        Task<IBindSource> InitBindAsync(Uri address, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"><param name="address"><see cref="Uri.HostNameType"/> must be <see cref="UriHostNameType.IPv4"/> or <see cref="UriHostNameType.IPv6"/></param></param>
        /// <returns></returns>
        Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address, CancellationToken cancellationToken = default);
    }
}
