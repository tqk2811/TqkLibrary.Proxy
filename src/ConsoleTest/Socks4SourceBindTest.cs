using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace ConsoleTest
{
    internal static class Socks4SourceBindTest
    {
        public static async Task RunAsync()
        {
            IProxySource proxySource = new LocalProxySource();
            using var bindSource = proxySource.GetBindSource();

            await bindSource.BindAsync();
            var endpoint = await bindSource.BindAsync();

            Task task_connect = ConnectAsync(endpoint);
            using var stream = await bindSource.GetStreamAsync();
            using StreamReader streamReader = new StreamReader(stream);
            string? line = streamReader.ReadLine();
            Console.WriteLine($"Received: {line}");
            Console.ReadLine();
        }
        static async Task WaitConnectAsync(IBindSource bindSource)
        {
            using var stream = await bindSource.GetStreamAsync();
            byte[] buff = new byte[1024];
            int byte_read = await stream.ReadAsync(buff, 0, buff.Length);
            Console.WriteLine($"Get: {Encoding.ASCII.GetString(buff, 0, byte_read)}");
        }
        static async Task ConnectAsync(IPEndPoint iPEndPoint)
        {
            using TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(iPEndPoint);
            using var stream = tcpClient.GetStream();
            byte[] data = "hello\r\n".Select(x => (byte)x).ToArray();
            await stream.WriteAsync(data, 0, data.Length);
        }
    }
}
