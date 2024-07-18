using System.Net;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServer
    {
        Task ProxyWorkAsync(Stream clientStream, IPEndPoint clientEndPoint, IProxyServerHandler proxyServerHandler, Guid tunnelId, CancellationToken cancellationToken = default);
    }
}
