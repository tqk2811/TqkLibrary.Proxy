using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.ProxyServers
{
    internal abstract class BaseProxyServerTunnel<T> where T : BaseProxyServer
    {
        protected readonly T _proxyServer;
        protected readonly Stream _clientStream;
        protected readonly EndPoint _clientEndPoint;
        protected readonly CancellationToken _cancellationToken;
        protected BaseProxyServerTunnel(T proxyServer, Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            this._proxyServer = proxyServer ?? throw new ArgumentNullException(nameof(proxyServer));
            this._clientStream = clientStream ?? throw new ArgumentNullException(nameof(clientStream));
            this._clientEndPoint = clientEndPoint ?? throw new ArgumentNullException(nameof(clientEndPoint));
            this._cancellationToken = cancellationToken;
        }

        internal abstract Task ProxyWorkAsync();
    }
}
