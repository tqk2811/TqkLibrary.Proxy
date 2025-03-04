using System.Net;
using TestProxy.ServerTest;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.HttpProxySourceTest
{
    [TestClass]
    public class HttpProxySourceTest : HttpProxyServerTest
    {
        IProxySource? _proxySource;
        ProxyServer? _proxyServer2;
        protected override IProxySource GetProxySource()
        {
            _proxySource = base.GetProxySource();
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
