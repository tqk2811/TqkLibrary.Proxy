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
        /// scheme: http/https, tcp, udp, ws/wss
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<IConnectionSource> InitConnectionAsync(Uri address, CancellationToken cancellationToken = default);

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        Task<IBindSource> InitBindAsync(CancellationToken cancellationToken = default);
    }
}
