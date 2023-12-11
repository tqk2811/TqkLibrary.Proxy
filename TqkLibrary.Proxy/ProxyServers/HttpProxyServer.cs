using System.Net;
using TqkLibrary.Proxy.Filters;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class HttpProxyServer : BaseProxyServer, IHttpProxy
    {
        public HttpProxyServerFilter Filter { get; }

        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource)
            : this(iPEndPoint, proxySource, new HttpProxyServerFilter())
        {

        }
        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, HttpProxyServerFilter httpProxyServerFilter) : base(iPEndPoint, proxySource, httpProxyServerFilter)
        {
            this.Filter = httpProxyServerFilter;
        }

        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new HttpProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken)
                .ProxyWorkAsync();
        }
    }
}
