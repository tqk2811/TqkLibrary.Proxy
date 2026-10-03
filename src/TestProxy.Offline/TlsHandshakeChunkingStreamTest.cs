using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHelpers;

namespace TestProxy.Offline
{
    /// <summary>
    /// The handshake records are written in chunks and everything after them in one piece, without
    /// a byte changed. The chunking is checked on a recording stream, since loopback can merge
    /// segments on the receive side.
    /// </summary>
    [TestClass]
    public class TlsHandshakeChunkingStreamTest
    {
        // A handshake record (content type 0x16) with a body of the given length.
        static byte[] HandshakeRecord(int bodyLength)
        {
            byte[] record = new byte[5 + bodyLength];
            record[0] = 0x16; record[1] = 0x03; record[2] = 0x01;
            record[3] = (byte)(bodyLength >> 8); record[4] = (byte)bodyLength;
            for (int i = 0; i < bodyLength; i++) record[5 + i] = (byte)(i + 1);
            return record;
        }

        static readonly byte[] ChangeCipherSpec = { 0x14, 0x03, 0x03, 0x00, 0x01, 0x01 };
        static readonly byte[] ApplicationData = { 0x17, 0x03, 0x03, 0x00, 0x03, 0xAA, 0xBB, 0xCC };

        static int[] Sizes(RecordingStream stream) => stream.Writes.Select(x => x.Length).ToArray();

        [TestMethod]
        public async Task HandshakeRecord_IsWrittenInChunks_AndWhatFollowsInOnePiece()
        {
            byte[] hello = HandshakeRecord(7); // 12 bytes
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 5);

            await stream.WriteAsync(hello, 0, hello.Length);
            Assert.IsFalse(stream.IsPassthrough);
            await stream.WriteAsync(ApplicationData, 0, ApplicationData.Length);

            CollectionAssert.AreEqual(new[] { 5, 5, 2, ApplicationData.Length }, Sizes(inner));
            Assert.IsTrue(stream.IsPassthrough);
            CollectionAssert.AreEqual(hello.Concat(ApplicationData).ToArray(), inner.Writes.SelectMany(x => x).ToArray());
        }

        [TestMethod]
        public async Task RecordSpreadOverWrites_AndTheTailSharingAWrite_AreSplitAtTheRecordBoundary()
        {
            // Header alone, then the body together with ChangeCipherSpec and application data, the
            // way a TLS 1.3 client flushes its second flight.
            byte[] hello = HandshakeRecord(4); // 9 bytes
            byte[] second = hello.Skip(5).Concat(ChangeCipherSpec).Concat(ApplicationData).ToArray();
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 2);

            await stream.WriteAsync(hello, 0, 5);
            await stream.WriteAsync(second, 0, second.Length);

            CollectionAssert.AreEqual(new[] { 2, 2, 1, 2, 2, ChangeCipherSpec.Length + ApplicationData.Length }, Sizes(inner));
            CollectionAssert.AreEqual(hello.Concat(ChangeCipherSpec).Concat(ApplicationData).ToArray(), inner.Writes.SelectMany(x => x).ToArray());
        }

        [TestMethod]
        public async Task TwoHandshakeRecordsInOneWrite_AreBothChunked()
        {
            byte[] records = HandshakeRecord(1).Concat(HandshakeRecord(1)).ToArray(); // 6 + 6
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 4);

            await stream.WriteAsync(records, 0, records.Length);

            CollectionAssert.AreEqual(new[] { 4, 4, 4 }, Sizes(inner));
            Assert.IsFalse(stream.IsPassthrough);
        }

        [TestMethod]
        public async Task NonTlsStream_IsPassedThroughFromTheStart()
        {
            byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 1);

            await stream.WriteAsync(request, 0, request.Length);

            CollectionAssert.AreEqual(new[] { request.Length }, Sizes(inner));
            Assert.IsTrue(stream.IsPassthrough);
        }

        [TestMethod]
        public void SyncAndSpanWrites_ChunkTheSameWay()
        {
            byte[] hello = HandshakeRecord(3); // 8 bytes
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 3);

            stream.Write(hello, 0, 4);
            stream.Write(hello.AsSpan(4));
            stream.Write(ApplicationData.AsSpan());

            CollectionAssert.AreEqual(new[] { 3, 1, 3, 1, ApplicationData.Length }, Sizes(inner));
        }

        [TestMethod]
        public void ChunkSize_RejectsZeroAndNegative()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new TlsHandshakeChunkingStream(new MemoryStream(), 0));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new HttpProxySource(new Uri("http://127.0.0.1:1")) { TlsHandshakeChunkSize = -1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Socks5ProxySource(new Uri("socks5://127.0.0.1:1")) { TlsHandshakeChunkSize = -1 });
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new Socks4ProxySource(new Uri("socks4://127.0.0.1:1")) { TlsHandshakeChunkSize = -1 });
        }

        [TestMethod]
        public async Task NoDelay_IsOnForTheHandshake_AndRestoredAfter()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using TcpClient client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            using TcpClient server = await listener.AcceptTcpClientAsync();
            client.NoDelay = false;

            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(client.GetStream(), 2, client.Client, disposeBaseStream: false);
            Assert.IsTrue(client.NoDelay);

            byte[] hello = HandshakeRecord(3);
            await stream.WriteAsync(hello, 0, hello.Length);
            Assert.IsTrue(client.NoDelay);

            await stream.WriteAsync(ApplicationData, 0, ApplicationData.Length);
            Assert.IsFalse(client.NoDelay);
        }

        [TestMethod]
        public async Task Relay_PassesTheFinOnThroughTheWrapper()
        {
            // The relay half-closes a NetworkStream itself; a decorator used to fall through that
            // check, so the upstream never learned the client had finished and sat there until it
            // timed out.
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using TcpClient client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            using TcpClient server = await listener.AcceptTcpClientAsync();

            using TlsHandshakeChunkingStream wrapped = new TlsHandshakeChunkingStream(client.GetStream(), 2, client.Client);
            using MemoryStream finishedClient = new MemoryStream(); // ends at once
            Task relay = new StreamTransferHelper(finishedClient, wrapped, Guid.NewGuid()).WaitUntilDisconnect();

            Task<int> read = server.GetStream().ReadAsync(new byte[16], 0, 16);
            Assert.AreSame(read, await Task.WhenAny(read, Task.Delay(5000)), "the upstream never saw a FIN");
            Assert.AreEqual(0, await read);

            server.Close();
            await Task.WhenAny(relay, Task.Delay(5000));
        }

        [TestMethod]
        [DataRow("http")]
        [DataRow("socks4")]
        [DataRow("socks5")]
        public async Task ThroughAProxy_TheTunnelCarriesTheSameBytes(string protocol)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            byte[] hello = HandshakeRecord(20);
            byte[] sent = hello.Concat(ApplicationData).ToArray();
            Task<byte[]> upstream = FakeUpstreamAsync(listener, protocol, sent.Length);

            IPEndPoint endPoint = (IPEndPoint)listener.LocalEndpoint;
            IProxySource source = protocol switch
            {
                "socks4" => new Socks4ProxySource(endPoint) { TlsHandshakeChunkSize = 3 },
                "socks5" => new Socks5ProxySource(endPoint) { TlsHandshakeChunkSize = 3 },
                _ => new HttpProxySource(new Uri($"http://{endPoint}")) { TlsHandshakeChunkSize = 3 },
            };
            using IConnectSource tunnel = await source.GetConnectSourceAsync(Guid.NewGuid());
            await tunnel.ConnectAsync(new Uri("tcp://example.com:443"));
            Stream stream = await tunnel.GetStreamAsync();
            Assert.IsInstanceOfType(stream, typeof(TlsHandshakeChunkingStream));

            await stream.WriteAsync(hello, 0, hello.Length);
            await stream.WriteAsync(ApplicationData, 0, ApplicationData.Length);

            CollectionAssert.AreEqual(sent, await upstream);
        }

        // Plays the proxy's side of CONNECT (HTTP, SOCKS4a or SOCKS5, no auth), then returns the
        // next `tunnelBytes` bytes the client sends through the tunnel.
        static async Task<byte[]> FakeUpstreamAsync(TcpListener listener, string protocol, int tunnelBytes)
        {
            using TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();
            switch (protocol)
            {
                case "socks4":
                    await ReadExactAsync(stream, 8); // VER CMD PORT IP
                    for (int nulls = 0; nulls < 2;) // USERID\0 DOMAIN\0
                        if ((await ReadExactAsync(stream, 1))[0] == 0) nulls++;
                    await stream.WriteAsync(new byte[] { 0x00, 0x5A, 0, 0, 0, 0, 0, 0 });
                    break;

                case "socks5":
                    byte[] greeting = await ReadExactAsync(stream, 2);
                    await ReadExactAsync(stream, greeting[1]);
                    await stream.WriteAsync(new byte[] { 0x05, 0x00 });
                    byte[] head = await ReadExactAsync(stream, 5);
                    await ReadExactAsync(stream, head[4] + 2);
                    await stream.WriteAsync(new byte[] { 0x05, 0x00, 0x00, 0x01, 0, 0, 0, 0, 0, 0 });
                    break;

                default:
                    List<byte> request = new List<byte>();
                    while (request.Count < 4 || Encoding.ASCII.GetString(request.Skip(request.Count - 4).ToArray()) != "\r\n\r\n")
                        request.AddRange(await ReadExactAsync(stream, 1));
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
                    break;
            }
            return await ReadExactAsync(stream, tunnelBytes);
        }

        static async Task<byte[]> ReadExactAsync(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int n = await stream.ReadAsync(buffer, read, count - read);
                if (n == 0) throw new EndOfStreamException();
                read += n;
            }
            return buffer;
        }

        sealed class RecordingStream : MemoryStream
        {
            public List<byte[]> Writes { get; } = new List<byte[]>();

            public override void Write(byte[] buffer, int offset, int count)
                => Writes.Add(buffer.Skip(offset).Take(count).ToArray());

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Write(buffer, offset, count);
                return Task.CompletedTask;
            }
        }
    }
}
