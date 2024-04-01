using System.Net;
using TestProxy.ServerTest;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.HttpProxySourceTest
{
    [TestClass]
    public class HttpProxySourceIpV6Test : HttpProxyServerIpV6Test
    {
        IProxySource? _proxySource;
        ProxyServer? _proxyServer2;
        protected override IProxySource GetProxySource()
        {
            _proxySource = base.GetProxySource();
            _proxyServer2 = new ProxyServer(IPEndPoint.Parse("[::1]:0"))
            {
                ProxyServerHandler = new BaseProxyServerHandler(_proxySource)
            };
            _proxyServer2.StartListen();

            return new HttpProxySource(new Uri($"http://{_proxyServer2.IPEndPoint}"), _networkCredential);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            _proxyServer2?.Dispose();
        }
    }
}
