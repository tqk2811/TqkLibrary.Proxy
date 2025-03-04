﻿using System.Net;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.ServerTest
{
    [TestClass]
    public class Socks5ProxyServerTest : BaseUdpTest
    {
        protected override IProxySource GetProxySource()
        {
            return new LocalProxySource();
        }
        protected override ProxyServer CreateServer(IProxySource proxySource)
        {
            return new ProxyServer(IPEndPoint.Parse("127.0.0.1:0"), proxySource);
        }
        protected override HttpMessageHandler CreateHttpMessageHandler(ProxyServer baseProxyServer)
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
        protected override IProxySource GetSocksProxySource(ProxyServer baseProxyServer)
        {
            return new Socks5ProxySource(baseProxyServer.IPEndPoint);
        }
    }
}
