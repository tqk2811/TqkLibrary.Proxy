using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.StreamHelpers;

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
        Task<bool> IsAcceptClientAsync(TcpClient tcpClient, Guid tunnelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// SSL/Cert or limit/calc bandwidth
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="iPEndPoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Stream> StreamHandlerAsync(Stream stream, IPEndPoint iPEndPoint, Guid tunnelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handler allow http/socks4/socks5 base on IP
        /// </summary>
        /// <param name="preReadStream"></param>
        /// <param name="iPEndPoint"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IProxyServer> GetProxyServerAsync(PreReadStream preReadStream, IPEndPoint iPEndPoint, Guid tunnelId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invoked when an uncaught exception escapes the tunnel work pipeline
        /// (anything inside ProxyServer's per-tunnel scope after IsAcceptClient returned true).
        /// Implementations can use this to mark the tunnel as failed in their own logs.
        /// </summary>
        /// <param name="iPEndPoint">Client endpoint.</param>
        /// <param name="tunnelId">Tunnel identifier — matches IsAcceptClient/StreamHandlerAsync/GetProxyServerAsync.</param>
        /// <param name="exception">The exception that escaped (already logged by ProxyServer).</param>
        Task OnExceptionAsync(IPEndPoint iPEndPoint, Guid tunnelId, Exception exception);
    }
}
