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
    public class Socks5ProxyServerTest : BaseUdpTest
    {
        protected override IProxySource GetProxySource()
        {
            return new LocalProxySource();
        }
        protected override BaseProxyServer CreateServer(IProxySource proxySource)
        {
            return new Socks5ProxyServer(IPEndPoint.Parse("127.0.0.1:0"), proxySource);
        }
        protected override HttpMessageHandler CreateHttpMessageHandler(BaseProxyServer baseProxyServer)
        {
            return new SocketsHttpHandler()
            {
                Proxy = new WebProxy()
                {
                    Address = new Uri($"socks5://{baseProxyServer.IPEndPoint}"),
                },
                UseCookies = false,
                UseProxy = true,
            };
        }
    }
}
