using ConsoleTest;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.GlobalUnicast;
using System.Net.NetworkInformation;

TqkLibrary.Proxy.Singleton.LoggerFactory = LoggerFactory.Create(x => x.AddConsole());

Uri uri0 = new Uri("http://127.0.0.1:13566");
Uri uri1 = new Uri("http://[::1]:13566");
Uri uri2 = new Uri("httpbin.org:80");//must ->http://httpbin.org:80
Uri uri3 = new Uri("http://httpbin.org");
Uri uri4 = new Uri("http://httpbin.org:8080");
Uri uri5 = new Uri("tcp://httpbin.org:8080");
Uri uri6 = new Uri("udp://httpbin.org:8080");
Uri uri7 = new Uri("tcp://127.0.0.1:13566");
Uri uri8 = new Uri("udp://[::1]:13566");
Uri uri9 = new Uri("http://[0:0:0:0:0:0:0:1]:13566");
Uri uri10 = new Uri("http://localhost:13566");

string strHostName = Dns.GetHostName();
Console.WriteLine("Local Machine's Host Name: " + strHostName);
IPHostEntry iPHostEntry = Dns.GetHostEntry(strHostName);
foreach (var item in iPHostEntry.AddressList)
{
    Console.WriteLine($"{item.AddressFamily}: {item}");
}

Console.WriteLine();
Console.WriteLine("UnicastAddresses");
foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
{
    var props = nic.GetIPProperties();
    foreach (var addr in props.UnicastAddresses)
    {
        if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            Console.WriteLine($"{nic.Name} => {addr.Address}");
        }
    }
}
//TcpListener tcpListener = new TcpListener(IPAddress.Any, 0);
//tcpListener.Start();

//await Socks4SourceBindTest.RunAsync();
await ProxyWraper.RunAsync();
//await DebugTest.Test();
//RealTest.HttpProxyServerTest();
