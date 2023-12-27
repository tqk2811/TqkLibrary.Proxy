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
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Authentications;

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
        protected override BaseProxyServer CreateServer(IProxySource proxySource)
        {
            HttpAuthenticationProxyServerHandler handler = new HttpAuthenticationProxyServerHandler(proxySource);
            handler.WithAuthentications(_networkCredential);
            return new HttpProxyServer(IPEndPoint.Parse("127.0.0.1:0"), handler);
        }
        protected override HttpMessageHandler CreateHttpMessageHandler(BaseProxyServer baseProxyServer)
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
