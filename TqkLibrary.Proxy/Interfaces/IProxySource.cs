using System.Net;
using System.Net.Sockets;

namespace TqkLibrary.Proxy.Interfaces
{
    //https://en.wikipedia.org/wiki/Proxy_server#Web_proxy_servers
    public class Endpoint
    {
        public ProtocolType ProtocolType { get; set; }
        public IPAddress IPAddress { get; set; } = IPAddress.Any;

    }
    public interface IProxySource
    {
        bool IsSupportUdp { get; }

        /// <summary>
        /// 
        /// </summary>
        bool IsSupportTransferHttps { get; }

        bool IsSupportIpv6 { get; }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        /// <param name="host">Host for SslStream</param>
        /// <returns></returns>
        Task<ISessionSource> InitSessionAsync(Uri address, string host = null);
    }
    /*
     *Sock4/Sock5 => IP
     *Https proxy: CONNECT domain:port or ip:port -> url
     *Http proxy: Get <full url or ip:port>
    */
}
