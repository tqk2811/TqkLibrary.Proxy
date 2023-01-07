using System.Net;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class HttpProxyServer : BaseProxyServer, IHttpProxy
    {
        public NetworkCredential Credentials { get; }
        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, NetworkCredential credentials = null) : base(iPEndPoint, proxySource)
        {
            this.Credentials = credentials;
        }

        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new HttpProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken)
                .ProxyWorkAsync();
        }
    }
}
