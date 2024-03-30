using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IPreProxyServerHandler
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> IsAcceptClientAsync(TcpClient tcpClient, CancellationToken cancellationToken = default);

        /// <summary>
        /// SSL/Cert or limit/calc bandwidth
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="iPEndPoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Stream> StreamHandlerAsync(Stream stream, IPEndPoint iPEndPoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handler allow http/socks4/socks5 base on IP
        /// </summary>
        /// <param name="preReadStream"></param>
        /// <param name="iPEndPoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IProxyServer> GetProxyServerAsync(PreReadStream preReadStream, IPEndPoint iPEndPoint, CancellationToken cancellationToken = default);
    }
}
