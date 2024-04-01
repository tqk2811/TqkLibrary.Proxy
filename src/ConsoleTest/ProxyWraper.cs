using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
        const string listen = "0.0.0.0:16615";
        public static async Task RunAsync()
        {
            //string strHostName = Dns.GetHostName();
            //IPHostEntry iPHostEntry = Dns.GetHostEntry(strHostName);
            //IPAddress? ipaddress = null;

            //ipaddress = iPHostEntry
            //        .AddressList
            //        .FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);

            IPEndPoint ipEndPoint = IPEndPoint.Parse(listen);
            //if (ipaddress is not null)
            //{
            //    ipEndPoint = new IPEndPoint(ipaddress, 0);
            //}

            //ipEndPoint = new IPEndPoint(IPAddress.Any, 0);

            using var server = new ProxyServer(ipEndPoint, GetProxySource());
            server.StartListen();
            Console.WriteLine($"Listening {server.IPEndPoint}");
            Console.ReadLine();
        }

        static IProxySource GetProxySource()
        {
            //HttpProxyAuthentication? auth = null;// new HttpProxyAuthentication("", "");
            //HttpProxySource httpProxySource = new HttpProxySource(new Uri("http://88.99.245.58:8903"));
            //Socks4ProxySource socks4ProxySource = new Socks4ProxySource(IPEndPoint.Parse("93.104.63.65:80"));
            //Socks5ProxySource socks5ProxySource = new Socks5ProxySource(IPEndPoint.Parse("138.201.120.118:29127"));
            LocalProxySource localProxySource = new LocalProxySource();

            return localProxySource;
        }
    }
}
