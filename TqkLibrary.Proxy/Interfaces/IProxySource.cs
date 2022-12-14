using System.Net;
using System.Net.Sockets;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxySource
    {
        bool IsSupportUdp { get; }
        bool IsSupportIpv6 { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        Task<ISessionSource> InitSessionAsync(Uri address);
    }
    /*
     *Sock4/Sock5 => IP
     *Https proxy: CONNECT domain:port or ip:port -> url
     *Http proxy: Get <full url or ip:port>
    */
}
