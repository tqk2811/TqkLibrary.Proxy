using System.Net;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.ServerTest
{
    //[TestClass]
    public class HttpProxyServerIpV6Test : BaseConnectTest
    {
        protected readonly NetworkCredential _networkCredential = new NetworkCredential("user", "password");
        protected override IProxySource GetProxySource()
        {
            return new LocalProxySource();
        }
        protected override ProxyServer CreateServer(IProxySource proxySource)
        {
            return new ProxyServer(IPEndPoint.Parse("[::1]:0"))
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
