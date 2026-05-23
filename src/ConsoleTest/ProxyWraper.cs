using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        public static async Task RunAsync(ILoggerFactory? loggerFactory = null)
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

            MyProxyServerHandler myProxyServerHandler = new(GetProxySource(loggerFactory));
            using var server = new ProxyServer(ipEndPoint, myProxyServerHandler, loggerFactory);
            server.PreProxyServerHandler = new MyBasePreProxyServerHandler(loggerFactory);
            server.StartListen();
            Console.WriteLine($"Listening {server.IPEndPoint}");
            Console.ReadLine();
        }

        static IProxySource GetProxySource(ILoggerFactory? loggerFactory = null)
        {
            ProxyCredential? auth = new ProxyCredential("hwac7m0f", "hWaC7m0F");
            IProxySource proxySource;
            //proxySource = new HttpProxySource(new Uri("http://15.204.2.117:31419"), auth, loggerFactory);
            //proxySource = new Socks4ProxySource(IPEndPoint.Parse("93.104.63.65:80"), loggerFactory: loggerFactory);
            //proxySource = new Socks5ProxySource(IPEndPoint.Parse("138.201.120.118:29127"), loggerFactory);
            proxySource = new LocalProxySource(loggerFactory);
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

            //public override Task<Stream> StreamHandlerAsync(Stream stream, IUserInfo userInfo, CancellationToken cancellationToken = default)
            //{
            //    ExchangeLimitStream exchangeLimitStream = new ExchangeLimitStream(stream, true);
            //    exchangeLimitStream.MaxBytesRead = 100 * 1024 * 1024;
            //    exchangeLimitStream.MaxBytesWrite = 100 * 1024 * 1024;
            //    return Task.FromResult<Stream>(exchangeLimitStream);
            //    //return base.StreamHandlerAsync(stream, userInfo, cancellationToken);
            //}

            public override Task<bool> IsAcceptUserAsync(IUserInfo userInfo, CancellationToken cancellationToken = default)
            {
                return base.IsAcceptUserAsync(userInfo, cancellationToken);
            }

            public override Task<bool> IsAcceptDomainAsync(Uri uri, IUserInfo userInfo, CancellationToken cancellationToken = default)
            {
                return base.IsAcceptDomainAsync(uri, userInfo, cancellationToken);
            }

            public override Task<IProxySource> GetProxySourceAsync(Uri? uri, IUserInfo userInfo, CancellationToken cancellationToken = default)
            {
                return base.GetProxySourceAsync(uri, userInfo, cancellationToken);
            }
        }

        class MyBasePreProxyServerHandler: BasePreProxyServerHandler
        {
            public MyBasePreProxyServerHandler(ILoggerFactory? loggerFactory = null) : base(loggerFactory)
            {
            }

            public override Task<bool> IsAcceptClientAsync(TcpClient tcpClient, Guid tunnelId, CancellationToken cancellationToken = default)
            {
                return base.IsAcceptClientAsync(tcpClient, tunnelId, cancellationToken);
            }
        }
    }
}
