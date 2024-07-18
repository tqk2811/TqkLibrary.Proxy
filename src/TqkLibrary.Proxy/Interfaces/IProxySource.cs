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
        IConnectSource GetConnectSource(Guid tunnelId);

        /// <summary>
        /// 
        /// </summary>
        IBindSource GetBindSource(Guid tunnelId);

        /// <summary>
        /// 
        /// </summary>
        IUdpAssociateSource GetUdpAssociateSource(Guid tunnelId);
    }
}
