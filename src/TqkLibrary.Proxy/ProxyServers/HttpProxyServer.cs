using System.Net;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class HttpProxyServer : BaseProxyServer, IHttpProxy
    {
        public HttpProxyServerHandler Handler { get; }

        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource)
            : this(iPEndPoint, proxySource, new HttpProxyServerHandler())
        {

        }
        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, HttpProxyServerHandler handler) 
            : base(iPEndPoint, proxySource, handler)
        {
            this.Handler = handler;
        }

        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new HttpProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken)
                .ProxyWorkAsync();
        }
    }
}
