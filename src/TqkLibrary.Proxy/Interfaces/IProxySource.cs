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
        IConnectSource GetConnectSource();

        /// <summary>
        /// 
        /// </summary>
        IBindSource GetBindSource();

        /// <summary>
        /// 
        /// </summary>
        IUdpAssociateSource GetUdpAssociateSource();
    }
}
