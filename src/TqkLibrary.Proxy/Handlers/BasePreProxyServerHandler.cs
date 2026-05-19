using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.Handlers
{
    public class BasePreProxyServerHandler : IPreProxyServerHandler
    {
        protected readonly ILoggerFactory? _loggerFactory;
        protected IProxyServerFactory ProxyServerFactory { get; set; }

        public BasePreProxyServerHandler(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            ProxyServerFactory = new DefaultProxyServerFactory(loggerFactory);
        }

        public virtual Task<bool> IsAcceptClientAsync(TcpClient tcpClient, Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
        public virtual Task<Stream> StreamHandlerAsync(Stream stream, IPEndPoint iPEndPoint, Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(stream);
        }

        public virtual Task<IProxyServer> GetProxyServerAsync(PreReadStream preReadStream, IPEndPoint iPEndPoint, Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return ProxyServerFactory.CreateAsync(preReadStream, cancellationToken);
        }
    }
}
