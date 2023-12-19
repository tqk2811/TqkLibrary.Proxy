using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;

namespace ConsoleTest
{
    internal static class ProxyWraper
    {
        const string listen = "127.0.0.1:16615";
        public static async Task RunAsync()
        {
            using var server = new HttpProxyServer(IPEndPoint.Parse(listen), GetProxySource());
            server.StartListen();
            Console.WriteLine($"Listening {listen}");
            Console.ReadLine();
        }

        static IProxySource GetProxySource()
        {
            HttpProxyAuthentication? auth = null;// new HttpProxyAuthentication("", "");
            HttpProxySource httpProxySource = new HttpProxySource(new Uri("http://167.99.142.56:20128"));
            Socks4ProxySource socks4ProxySource = new Socks4ProxySource(IPEndPoint.Parse("93.104.63.65:80"));
            Socks5ProxySource socks5ProxySource = new Socks5ProxySource(IPEndPoint.Parse("138.201.120.118:29127"));
            LocalProxySource localProxySource = new LocalProxySource();

            return localProxySource;
        }
    }
}
