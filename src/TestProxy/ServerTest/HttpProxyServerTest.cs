using System.Net;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.ServerTest
{
    //[TestClass]
    public class HttpProxyServerTest : BaseConnectTest
    {

        protected readonly NetworkCredential _networkCredential = new NetworkCredential("user", "password");
        protected override IProxySource GetProxySource()
        {
            return new LocalProxySource();
        }
        protected override ProxyServer CreateServer(IProxySource proxySource)
        {
            return new ProxyServer(IPEndPoint.Parse("127.0.0.1:0"))
            {
                ProxyServerHandler = new CustomHttpProxyServerHandler(proxySource, _networkCredential)
            };
        }
        protected override HttpMessageHandler CreateHttpMessageHandler(ProxyServer baseProxyServer)
        {
            return new HttpClientHandler()
            {
                Proxy = new WebProxy()
                {
                    Address = new Uri($"http://{baseProxyServer.IPEndPoint}"),
                },
                UseCookies = false,
                UseProxy = true,
                DefaultProxyCredentials = _networkCredential,
            };
        }
    }
}
