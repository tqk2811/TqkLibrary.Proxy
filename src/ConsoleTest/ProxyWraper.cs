using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.GlobalUnicast;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Streams;

namespace ConsoleTest
{
    internal static class ProxyWraper
    {
        const string listen = "0.0.0.0:28111";
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

            MyProxyServerHandler myProxyServerHandler = new(GetProxySource());
            using var server = new ProxyServer(ipEndPoint, myProxyServerHandler);
            server.StartListen();
            Console.WriteLine($"Listening {server.IPEndPoint}");
            Console.ReadLine();
        }

        static IProxySource GetProxySource()
        {
            HttpProxyAuthentication? auth = new HttpProxyAuthentication("0FKGiplus.", "VnNMVtbn");
            IProxySource proxySource;
            proxySource = new HttpProxySource(new Uri("http://103.171.1.93:8549"), auth);
            //proxySource = new Socks4ProxySource(IPEndPoint.Parse("93.104.63.65:80"));
            //proxySource = new Socks5ProxySource(IPEndPoint.Parse("138.201.120.118:29127"));
            //proxySource = new LocalProxySource();
            //proxySource = new GlobalUnicastProxySource()
            //{
            //    LifeTime = TimeSpan.FromMinutes(10),
            //};
            return proxySource;
        }

        class MyProxyServerHandler : BaseProxyServerHandler
        {
            public MyProxyServerHandler(IProxySource proxySource) : base(proxySource)
            {

            }

            public override Task<Stream> StreamHandlerAsync(Stream stream, IUserInfo userInfo, CancellationToken cancellationToken = default)
            {
                ExchangeLimitStream exchangeLimitStream = new ExchangeLimitStream(stream, true);
                exchangeLimitStream.MaxBytesRead = 100 * 1024 * 1024;
                exchangeLimitStream.MaxBytesWrite = 100 * 1024 * 1024;
                return Task.FromResult<Stream>(exchangeLimitStream);
                //return base.StreamHandlerAsync(stream, userInfo, cancellationToken);
            }
        }
    }
}
