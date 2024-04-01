using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;

namespace ConsoleTest
{
    internal static class RealTest
    {
        const string address = "127.0.0.1:13566";
        public static void HttpProxyServerTest()
        {
            //IProxySource proxySource = new LocalProxySource();
            //IProxySource proxySource = new HttpProxySource(new Uri("http://103.178.231.186:10003"));
            IProxySource proxySource = new HttpProxySource(new Uri("http://svhn1.proxyno1.com:41352"));
            ProxyServer proxyServer = new ProxyServer(IPEndPoint.Parse(address), proxySource);
            proxyServer.StartListen();
            Console.WriteLine("server started");
            Console.ReadLine();
            //httpProxyServer.ChangeSource(new HttpProxySource(new Uri("http://103.178.231.186:10003")), true);
            Console.ReadLine();
            proxyServer.StopListen();
            Console.ReadLine();
        }

        const string address2 = "117.2.46.2:5678";
        public static void Socks4ProxySourceTest()
        {
            //IProxySource proxySource = new LocalProxySource();
            //IProxySource proxySource = new HttpProxySource(new Uri("http://103.178.231.186:10003"));


            //IProxySource proxySource = new HttpProxySource(new Uri("http://svhn1.proxyno1.com:41352"),new NetworkCredential("user","pass"));
            //HttpProxyServer httpProxyServer = new HttpProxyServer(IPEndPoint.Parse("127.0.0.1:13566"), proxySource);
            //httpProxyServer.StartListen();

            //Console.WriteLine("server started");
            //Console.ReadLine();
            //httpProxyServer.ChangeSource(new HttpProxySource(new Uri("http://103.178.231.186:10003")), true);
            //Console.ReadLine();
            //httpProxyServer.StopListen();
            //Console.ReadLine();



            IProxySource proxySource = new Socks4ProxySource(IPEndPoint.Parse("212.213.132.5:31632"));//địa chỉ sock4
            ProxyServer proxyServer = new ProxyServer(IPEndPoint.Parse("127.0.0.1:13566"), proxySource);//địa chỉ http proxy host lại

            proxyServer.StartListen();//xài

            //code..... dùng proxy 127.0.0.1:13566

            proxyServer.StopListen();//xài xong nhớ tắt


            Console.ReadLine();
        }
    }
}
