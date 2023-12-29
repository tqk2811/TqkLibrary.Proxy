using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.ProxyServers;
using Newtonsoft.Json;

namespace TestProxy.ServerTest
{
    [TestClass]
    public class Socks4ProxyServerTest : BaseBindTest
    {
        protected override IProxySource GetProxySource()
        {
            return new LocalProxySource();
        }
        protected override BaseProxyServer CreateServer(IProxySource proxySource)
        {
            return new Socks4ProxyServer(IPEndPoint.Parse("127.0.0.1:0"), proxySource);
        }
        protected override HttpMessageHandler CreateHttpMessageHandler(BaseProxyServer baseProxyServer)
        {
            return new SocketsHttpHandler()
            {
                Proxy = new WebProxy()
                {
                    Address = new Uri($"socks4://{baseProxyServer.IPEndPoint}"),
                },
                UseCookies = false,
                UseProxy = true,
            };
        }
        protected override IProxySource GetSocksProxySource(BaseProxyServer baseProxyServer)
        {
            return new Socks4ProxySource(baseProxyServer.IPEndPoint);
        }
    }
}
