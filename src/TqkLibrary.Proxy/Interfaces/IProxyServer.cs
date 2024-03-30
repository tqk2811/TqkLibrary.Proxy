using System.Net;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServer
    {
        Task ProxyWorkAsync(Stream clientStream, IPEndPoint clientEndPoint, IProxyServerHandler proxyServerHandler, CancellationToken cancellationToken = default);
    }
}
