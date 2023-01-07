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

        protected override Task ProxyWorkAsync(Stream client_stream, EndPoint client_EndPoint, CancellationToken cancellationToken = default)
        {
            return new HttpProxyServerTunnel(this, client_stream, client_EndPoint, cancellationToken)
                .ProxyWorkAsync();
        }
    }
}
