using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;

namespace TestProxy
{
    public abstract class BaseBindTest : BaseConnectTest
    {
        readonly IProxySource _sockProxySource;
        public BaseBindTest() : base()
        {
            _sockProxySource = GetSocksProxySource(_proxyServer);
        }
        protected abstract IProxySource GetSocksProxySource(ProxyServer baseProxyServer);

        // [local source] <-> [socks server] <-> [socks source] <-> [IPEndPoint -> TcpClient]
        [TestMethod]
        public async Task TestBindTransfer()
        {
            using IBindSource bindSource = _sockProxySource.GetBindSource();
            IPEndPoint iPEndPoint = await bindSource.BindAsync();

            Task<string> t_ping = ConnectBindAsync(iPEndPoint);

            using var stream = await bindSource.GetStreamAsync();

            byte[] data = "ping\r\n".Select(x => (byte)x).ToArray();
            _ = stream.WriteAsync(data, 0, data.Length);

            using StreamReader streamReader = new StreamReader(stream);
            string pong = await streamReader.ReadLineAsync();


            Assert.AreEqual("pong", pong);
            Assert.AreEqual("ping", await t_ping);
        }


        async Task<string> ConnectBindAsync(IPEndPoint ipEndPoint)
        {
            using TcpClient tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(ipEndPoint);
            using Stream stream = tcpClient.GetStream();

            byte[] data = "pong\r\n".Select(x => (byte)x).ToArray();
            _ = stream.WriteAsync(data, 0, data.Length);

            using StreamReader streamReader = new StreamReader(stream);
            string ping = await streamReader.ReadLineAsync();
            return ping;
        }
    }
}
