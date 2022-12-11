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
        /// For sock4/sock5 or https proxy with ip:port (only tcp)
        /// </summary>
        /// <param name="iPAddress">ipv4/ipv6, ipv6 only for sock5</param>
        /// <param name="protocolType">only tcp/udp, udp only for sock5</param>
        /// <returns></returns>
        Task<ISessionSource> InitSessionAsync(IPAddress iPAddress, ProtocolType protocolType);
        /// <summary>
        /// For Http-proxy/Https-proxy
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
