using System.Net;
using TestProxy.ServerTest;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.GlobalUnicast;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.HttpProxySourceTest
{
    [TestClass]
    public class GlobalUnicastProxySourceTest : HttpProxyServerTest
    {
        IProxySource? _proxySource;
        ProxyServer? _proxyServer2;
        protected override IProxySource GetProxySource()
        {
            _proxySource = new GlobalUnicastProxySource()
            {
                LifeTime = TimeSpan.FromMinutes(10)
            };
            _proxyServer2 = new ProxyServer(IPEndPoint.Parse("127.0.0.1:0"), _proxySource);
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
