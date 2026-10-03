using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHelpers;

namespace TestProxy.Offline
{
    /// <summary>
    /// ConnectRequestChunkSize must change how the CONNECT request is written, never what is written:
    /// the upstream has to receive exactly the bytes it gets with it at 0. These run against a
    /// fake upstream on loopback, so they need no network. Loopback can merge segments on the receive
    /// side, so the chunk sizes are checked on a recording stream instead.
    /// </summary>
    [TestClass]
    public class ConnectRequestChunkSizeTest
    {
        static readonly Uri Target = new Uri("tcp://example.com:443");

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(5)]
        public async Task WriteInChunks_SplitsIntoChunksOfTheGivenSize(int chunkSize)
        {
            // 32 bytes: with 5 the last chunk is a short one (2 bytes).
            byte[] data = Encoding.ASCII.GetBytes("CONNECT example.com:443 HTTP/1.1");
            using RecordingStream stream = new RecordingStream();

            await stream.WriteInChunksAsync(data, 0, data.Length, chunkSize);

            int[] expectedSizes = Enumerable.Range(0, (data.Length + chunkSize - 1) / chunkSize)
                .Select(i => Math.Min(chunkSize, data.Length - i * chunkSize)).ToArray();
            CollectionAssert.AreEqual(expectedSizes, stream.Writes.Select(x => x.Length).ToArray());
            CollectionAssert.AreEqual(data, stream.Writes.SelectMany(x => x).ToArray());
        }

        [TestMethod]
        public async Task WriteSplitAround_ChunksOnlyTheGivenSpan()
        {
            byte[] data = Encoding.ASCII.GetBytes("CONNECT example.com:443 HTTP/1.1\r\n\r\n");
            using RecordingStream stream = new RecordingStream();

            await stream.WriteSplitAroundAsync(data, 0, data.Length, 8, "example.com".Length, 2);

            string[] writes = stream.Writes.Select(x => Encoding.ASCII.GetString(x)).ToArray();
            CollectionAssert.AreEqual(
                new[] { "CONNECT ", "ex", "am", "pl", "e.", "co", "m", ":443 HTTP/1.1\r\n\r\n" },
                writes);
        }

        [TestMethod]
        public void ChunkSize_RejectsNegative()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new HttpProxySource(new Uri("http://127.0.0.1:1")) { ConnectRequestChunkSize = -1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Socks5ProxySource(new Uri("socks5://127.0.0.1:1")) { ConnectRequestChunkSize = -1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Socks4ProxySource(new Uri("socks4://127.0.0.1:1")) { ConnectRequestChunkSize = -1 });
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task HttpConnect_SendsTheSameBytes(int chunkSize)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Task<byte[]> upstream = FakeHttpUpstreamAsync(listener);

            HttpProxySource source = new HttpProxySource(new Uri($"http://{listener.LocalEndpoint}"))
            {
                Credential = new ProxyCredential("user", "pass"),
                ConnectRequestChunkSize = chunkSize,
            };
            using IConnectSource tunnel = await source.GetConnectSourceAsync(Guid.NewGuid());
            await tunnel.ConnectAsync(Target);

            string expected = "CONNECT example.com:443 HTTP/1.1\r\n"
                + $"Proxy-Authorization: Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"))}\r\n\r\n";
            Assert.AreEqual(expected, Encoding.ASCII.GetString(await upstream));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task Socks5Connect_SendsTheSameBytes(int chunkSize)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Task<byte[]> upstream = FakeSocks5UpstreamAsync(listener);

            Socks5ProxySource source = new Socks5ProxySource((IPEndPoint)listener.LocalEndpoint)
            {
                ConnectRequestChunkSize = chunkSize,
            };
            using IConnectSource tunnel = await source.GetConnectSourceAsync(Guid.NewGuid());
            await tunnel.ConnectAsync(Target);

            byte[] name = Encoding.ASCII.GetBytes("example.com");
            byte[] expected = new byte[] { 0x05, 0x01, 0x00, 0x03, (byte)name.Length }
                .Concat(name).Concat(new byte[] { 0x01, 0xBB }).ToArray();
            CollectionAssert.AreEqual(expected, await upstream);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public async Task Socks4aConnect_SendsTheSameBytes(int chunkSize)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Task<byte[]> upstream = FakeSocks4UpstreamAsync(listener);

            Socks4ProxySource source = new Socks4ProxySource((IPEndPoint)listener.LocalEndpoint, "id")
            {
                ConnectRequestChunkSize = chunkSize,
            };
            using IConnectSource tunnel = await source.GetConnectSourceAsync(Guid.NewGuid());
            await tunnel.ConnectAsync(Target);

            // VER CMD PORT, the 0.0.0.x marker that says a name follows, USERID\0, DOMAIN\0.
            byte[] expected = new byte[] { 0x04, 0x01, 0x01, 0xBB, 0, 0, 0, 1 }
                .Concat(Encoding.ASCII.GetBytes("id\0example.com\0")).ToArray();
            CollectionAssert.AreEqual(expected, await upstream);
        }

        // Reads one SOCKS4a CONNECT (header, then the two NUL-terminated strings), grants it, and
        // returns the request bytes.
        static async Task<byte[]> FakeSocks4UpstreamAsync(TcpListener listener)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();
            List<byte> request = new List<byte>();
            for (int i = 0; i < 8; i++) request.Add(await ReadByteAsync(stream));
            for (int nulls = 0; nulls < 2;)
            {
                byte b = await ReadByteAsync(stream);
                request.Add(b);
                if (b == 0) nulls++;
            }
            await stream.WriteAsync(new byte[] { 0x00, 0x5A, 0, 0, 0, 0, 0, 0 });
            await stream.FlushAsync();
            return request.ToArray();
        }

        // Reads one request up to its blank line, answers 200, returns what was read.
        static async Task<byte[]> FakeHttpUpstreamAsync(TcpListener listener)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();
            List<byte> received = new List<byte>();
            while (!EndsWith(received, "\r\n\r\n"))
                received.Add(await ReadByteAsync(stream));
            byte[] reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
            await stream.WriteAsync(reply);
            await stream.FlushAsync();
            return received.ToArray();
        }

        // Answers the greeting with "no auth", then reads one domain-name CONNECT, grants it, and
        // returns the CONNECT bytes.
        static async Task<byte[]> FakeSocks5UpstreamAsync(TcpListener listener)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();

            await ReadByteAsync(stream); // VER
            int methods = await ReadByteAsync(stream);
            for (int i = 0; i < methods; i++) await ReadByteAsync(stream);
            await stream.WriteAsync(new byte[] { 0x05, 0x00 });

            List<byte> request = new List<byte>();
            for (int i = 0; i < 5; i++) request.Add(await ReadByteAsync(stream)); // VER CMD RSV ATYP LEN
            int rest = request[4] + 2;
            for (int i = 0; i < rest; i++) request.Add(await ReadByteAsync(stream));

            await stream.WriteAsync(new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 });
            await stream.FlushAsync();
            return request.ToArray();
        }

        static async Task<byte> ReadByteAsync(Stream stream)
        {
            byte[] one = new byte[1];
            if (await stream.ReadAsync(one) == 0) throw new EndOfStreamException();
            return one[0];
        }

        static bool EndsWith(List<byte> data, string tail)
        {
            if (data.Count < tail.Length) return false;
            for (int i = 0; i < tail.Length; i++)
                if (data[data.Count - tail.Length + i] != tail[i]) return false;
            return true;
        }

        sealed class RecordingStream : MemoryStream
        {
            public List<byte[]> Writes { get; } = new List<byte[]>();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Writes.Add(buffer.Skip(offset).Take(count).ToArray());
                return Task.CompletedTask;
            }
        }
    }
}
