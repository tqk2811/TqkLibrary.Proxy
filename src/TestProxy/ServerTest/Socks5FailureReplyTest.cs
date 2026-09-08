using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.ServerTest
{
    /// <summary>
    /// What a SOCKS5 client is told when the connection it asked for cannot be made, and what an
    /// IPv6 destination turns into on the way.
    /// </summary>
    [TestClass]
    public class Socks5FailureReplyTest
    {
        ProxyServer? _proxy;

        [TestInitialize]
        public void Start()
        {
            _proxy = new ProxyServer(IPEndPoint.Parse("127.0.0.1:0"), new LoopbackAllowingHandler());
            _proxy.StartListen();
        }

        [TestCleanup]
        public void Stop() => _proxy?.Dispose();

        // An upstream that refuses is the everyday failure — a port with nothing listening on it.
        // The protocol has a reply that says so; without one the client sees a socket that simply
        // ended, which it cannot tell apart from the proxy itself being broken.
        [TestMethod]
        public async Task A_refused_upstream_comes_back_as_a_reply_not_a_dropped_connection()
        {
            int deadPort = FindAClosedPort();

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _proxy!.IPEndPoint!.Port);
            using NetworkStream stream = client.GetStream();

            await GreetAsync(stream);
            // CONNECT 127.0.0.1:deadPort, as an IPv4 address (ATYP 0x01).
            await stream.WriteAsync(new byte[]
            {
                0x05, 0x01, 0x00, 0x01,
                127, 0, 0, 1,
                (byte)(deadPort >> 8), (byte)deadPort,
            });
            await stream.FlushAsync();

            byte[] reply = await ReadExactlyAsync(stream, 10);

            Assert.AreEqual(0x05, reply[0], "the reply is not a SOCKS5 one");
            Assert.AreNotEqual((byte)Socks5_STATUS.RequestGranted, reply[1], "a refused connection was reported as granted");
        }

        // ATYP 0x04 carries an IPv6 literal, and a URI needs it in brackets or its colons read as
        // the port separator. Building it without them threw before the connection was attempted,
        // so the server could not carry an IPv6 CONNECT at all.
        [TestMethod]
        public void An_ipv6_destination_becomes_a_usable_uri()
        {
            Socks5_Request request = Socks5_Request.CreateConnect(new Uri("tcp://[2001:db8::1]:443"));

            Uri uri = request.Uri;

            Assert.AreEqual(443, uri.Port);
            Assert.AreEqual(IPAddress.Parse("2001:db8::1"), IPAddress.Parse(uri.Host.Trim('[', ']')));
        }

        static async Task GreetAsync(NetworkStream stream)
        {
            await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 });   // one method: no auth
            await stream.FlushAsync();
            await ReadExactlyAsync(stream, 2);
        }

        static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int total = 0;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (total < count)
            {
                int read = await stream.ReadAsync(buffer, total, count - total, deadline.Token);
                if (read <= 0) throw new IOException($"the connection ended after {total} of {count} byte(s)");
                total += read;
            }
            return buffer;
        }

        static int FindAClosedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        sealed class LoopbackAllowingHandler : BaseProxyServerHandler
        {
            public LoopbackAllowingHandler() : base(new LocalProxySource()) { }

            public override Task<bool> IsAcceptDomainAsync(Uri uri, IUserInfo userInfo, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }
    }
}
