using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHelpers;

namespace TestProxy.Offline
{
    /// <summary>
    /// The ClientHello is re-framed into several TLS records around the SNI host name and every
    /// byte after it is passed through as it came. Writes are checked on a recording stream, since
    /// loopback can merge segments on the receive side.
    /// </summary>
    [TestClass]
    public class TlsHandshakeChunkingStreamTest
    {
        const string Host = "example.com";

        // A ClientHello record carrying an SNI for `host` plus one extension after it, so the
        // name sits in the middle of the body.
        static byte[] ClientHelloRecord(string host)
        {
            byte[] name = Encoding.ASCII.GetBytes(host);
            List<byte> ext = new List<byte>();
            // server_name: type 0, length, list length, name_type 0, name length, name
            ext.AddRange(new byte[] { 0x00, 0x00 });
            ext.AddRange(U16(name.Length + 5));
            ext.AddRange(U16(name.Length + 3));
            ext.Add(0x00);
            ext.AddRange(U16(name.Length));
            ext.AddRange(name);
            // supported_versions (0x002b) with TLS 1.3
            ext.AddRange(new byte[] { 0x00, 0x2B, 0x00, 0x03, 0x02, 0x03, 0x04 });

            List<byte> hello = new List<byte>();
            hello.AddRange(new byte[] { 0x03, 0x03 });          // legacy_version
            hello.AddRange(Enumerable.Range(1, 32).Select(i => (byte)i)); // random
            hello.Add(0x00);                                      // session id
            hello.AddRange(new byte[] { 0x00, 0x02, 0x13, 0x01 }); // one cipher suite
            hello.AddRange(new byte[] { 0x01, 0x00 });            // null compression
            hello.AddRange(U16(ext.Count));
            hello.AddRange(ext);

            List<byte> body = new List<byte> { 0x01, 0x00, (byte)(hello.Count >> 8), (byte)hello.Count };
            body.AddRange(hello);

            List<byte> record = new List<byte> { 0x16, 0x03, 0x01 };
            record.AddRange(U16(body.Count));
            record.AddRange(body);
            return record.ToArray();
        }

        static byte[] U16(int value) => new[] { (byte)(value >> 8), (byte)value };

        static readonly byte[] ApplicationData = { 0x17, 0x03, 0x03, 0x00, 0x03, 0xAA, 0xBB, 0xCC };

        // Puts the handshake bytes of a run of records back together, the way the server does,
        // and checks every record is a handshake record with the original version.
        static byte[] Reassemble(IEnumerable<byte[]> records)
        {
            List<byte> body = new List<byte>();
            foreach (byte[] record in records)
            {
                Assert.AreEqual((byte)0x16, record[0]);
                Assert.AreEqual((byte)0x03, record[1]);
                Assert.AreEqual((byte)0x01, record[2]);
                Assert.AreEqual(record.Length - 5, (record[3] << 8) | record[4]);
                body.AddRange(record.Skip(5));
            }
            return body.ToArray();
        }

        [TestMethod]
        public void TryFindServerName_FindsTheHost()
        {
            byte[] record = ClientHelloRecord(Host);
            byte[] body = record.Skip(5).ToArray();

            Assert.IsTrue(TlsHandshakeChunkingStream.TryFindServerName(body, out int start, out int length));
            Assert.AreEqual(Host, Encoding.ASCII.GetString(body, start, length));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(4)]
        public async Task ClientHello_IsSplitIntoRecordsOnlyAroundTheHost(int chunkSize)
        {
            byte[] hello = ClientHelloRecord(Host);
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, chunkSize);

            await stream.WriteAsync(hello, 0, hello.Length);
            Assert.IsTrue(stream.IsPassthrough);

            // Before the host, the host in pieces, after the host.
            int pieces = (Host.Length + chunkSize - 1) / chunkSize;
            Assert.AreEqual(pieces + 2, inner.Writes.Count);
            byte[][] hostRecords = inner.Writes.Skip(1).Take(pieces).ToArray();
            Assert.IsTrue(hostRecords.All(x => x.Length - 5 <= chunkSize));
            Assert.AreEqual(Host, Encoding.ASCII.GetString(hostRecords.SelectMany(x => x.Skip(5)).ToArray()));
            // The first record ends right where the name starts: no record carries all of it.
            Assert.IsFalse(Encoding.ASCII.GetString(inner.Writes[0]).Contains(Host));
            CollectionAssert.AreEqual(hello.Skip(5).ToArray(), Reassemble(inner.Writes));
        }

        [TestMethod]
        public async Task RecordSpreadOverWrites_IsHeldUntilComplete_AndWhatFollowsPassesThrough()
        {
            byte[] hello = ClientHelloRecord(Host);
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 2);

            await stream.WriteAsync(hello, 0, 3); // part of the header
            await stream.WriteAsync(hello, 3, 20);
            Assert.AreEqual(0, inner.Writes.Count);
            Assert.IsFalse(stream.IsPassthrough);

            // The rest of the record together with application data, in one write.
            byte[] rest = hello.Skip(23).Concat(ApplicationData).ToArray();
            await stream.WriteAsync(rest, 0, rest.Length);

            CollectionAssert.AreEqual(ApplicationData, inner.Writes[^1]);
            CollectionAssert.AreEqual(hello.Skip(5).ToArray(), Reassemble(inner.Writes.Take(inner.Writes.Count - 1)));
        }

        [TestMethod]
        public async Task HandshakeRecordWithoutSni_GoesOutAsItIs()
        {
            byte[] record = { 0x16, 0x03, 0x01, 0x00, 0x04, 0x02, 0x00, 0x00, 0x00 }; // not a ClientHello
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 1);

            await stream.WriteAsync(record, 0, record.Length);

            Assert.AreEqual(1, inner.Writes.Count);
            CollectionAssert.AreEqual(record, inner.Writes[0]);
            Assert.IsTrue(stream.IsPassthrough);
        }

        [TestMethod]
        public async Task StartingWith0x16ButNoTlsHeader_IsNotHeldBack()
        {
            // 0x16 then a version byte that is not 0x03: a protocol that merely begins with that byte.
            byte[] data = { 0x16, 0x01, 0x02, 0xFF, 0xFF, 0x09 };
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 1);

            await stream.WriteAsync(data, 0, data.Length);

            CollectionAssert.AreEqual(data, inner.Writes.SelectMany(x => x).ToArray());
            Assert.IsTrue(stream.IsPassthrough);
        }

        [TestMethod]
        public async Task RealTlsServer_AcceptsTheSplitClientHello()
        {
            // Byte comparisons cannot say a server takes a ClientHello split over records; a real
            // TLS stack on the other end can.
            using var key = System.Security.Cryptography.RSA.Create(2048);
            var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
                "CN=" + Host, key, System.Security.Cryptography.HashAlgorithmName.SHA256,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);
            using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            // SChannel wants a certificate whose key it can load, not an ephemeral one.
            using var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(ephemeral.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pfx));

            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            Task<string> server = Task.Run(async () =>
            {
                using TcpClient accepted = await listener.AcceptTcpClientAsync();
                using var ssl = new System.Net.Security.SslStream(accepted.GetStream());
                await ssl.AuthenticateAsServerAsync(certificate);
                byte[] buffer = new byte[5];
                int read = 0;
                while (read < 5) read += await ssl.ReadAsync(buffer, read, 5 - read);
                return Encoding.ASCII.GetString(buffer);
            });

            LocalProxySource source = new LocalProxySource { TlsHandshakeChunkSize = 2 };
            using IConnectSource tunnel = await source.GetConnectSourceAsync(Guid.NewGuid());
            await tunnel.ConnectAsync(new Uri($"tcp://{listener.LocalEndpoint}"));
            using var client = new System.Net.Security.SslStream(await tunnel.GetStreamAsync(), false, (_, _, _, _) => true);
            await client.AuthenticateAsClientAsync(Host);
            await client.WriteAsync(Encoding.ASCII.GetBytes("hello"));
            await client.FlushAsync();

            Task done = await Task.WhenAny(server, Task.Delay(10000));
            Assert.AreSame(server, done, "the handshake never completed");
            Assert.AreEqual("hello", await server);
        }

        [TestMethod]
        public async Task NonTlsStream_IsPassedThroughFromTheStart()
        {
            byte[] request = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n\r\n");
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 1);

            await stream.WriteAsync(request, 0, request.Length);

            CollectionAssert.AreEqual(new[] { request.Length }, inner.Writes.Select(x => x.Length).ToArray());
            Assert.IsTrue(stream.IsPassthrough);
        }

        [TestMethod]
        public void SyncAndSpanWrites_SplitTheSameWay()
        {
            byte[] hello = ClientHelloRecord(Host);
            RecordingStream inner = new RecordingStream();
            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 3);

            stream.Write(hello, 0, 10);
            stream.Write(hello.AsSpan(10));
            stream.Write(ApplicationData.AsSpan());

            Assert.AreEqual(1 + 4 + 1 + 1, inner.Writes.Count); // before, "exa" "mpl" "e.c" "om", after, app data
            CollectionAssert.AreEqual(hello.Skip(5).ToArray(), Reassemble(inner.Writes.Take(6)));
        }

        [TestMethod]
        public void IncompleteRecord_IsWrittenAsItIsOnDispose()
        {
            byte[] hello = ClientHelloRecord(Host);
            RecordingStream inner = new RecordingStream();
            TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(inner, 2, disposeBaseStream: false);

            stream.Write(hello, 0, 10);
            stream.Dispose();

            CollectionAssert.AreEqual(hello.Take(10).ToArray(), inner.Writes.SelectMany(x => x).ToArray());
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
        public async Task NoDelay_IsOnForTheClientHello_AndRestoredAfter()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            using TcpClient client = new TcpClient();
            await client.ConnectAsync((IPEndPoint)listener.LocalEndpoint);
            using TcpClient server = await listener.AcceptTcpClientAsync();
            client.NoDelay = false;

            using TlsHandshakeChunkingStream stream = new TlsHandshakeChunkingStream(client.GetStream(), 2, client.Client, disposeBaseStream: false);
            Assert.IsTrue(client.NoDelay);

            byte[] hello = ClientHelloRecord(Host);
            await stream.WriteAsync(hello, 0, 10);
            Assert.IsTrue(client.NoDelay);

            await stream.WriteAsync(hello, 10, hello.Length - 10);
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
        public async Task ThroughAProxy_TheServerGetsTheSameHandshake(string protocol)
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            byte[] hello = ClientHelloRecord(Host);
            int pieces = (Host.Length + 2) / 3;
            int wireLength = hello.Length + 5 * (pieces + 1) + ApplicationData.Length; // a header per extra record
            Task<byte[]> upstream = FakeUpstreamAsync(listener, protocol, wireLength);

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

            byte[] received = await upstream;
            CollectionAssert.AreEqual(ApplicationData, received.Skip(received.Length - ApplicationData.Length).ToArray());
            CollectionAssert.AreEqual(hello.Skip(5).ToArray(), Reassemble(SplitRecords(received.Take(received.Length - ApplicationData.Length).ToArray())));
        }

        [TestMethod]
        public async Task Direct_TheServerGetsTheSameHandshake()
        {
            using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            byte[] hello = ClientHelloRecord(Host);
            int pieces = (Host.Length + 1) / 2;
            int wireLength = hello.Length + 5 * (pieces + 1);
            Task<byte[]> server = Task.Run(async () =>
            {
                using TcpClient client = await listener.AcceptTcpClientAsync();
                return await ReadExactAsync(client.GetStream(), wireLength);
            });

            LocalProxySource source = new LocalProxySource { TlsHandshakeChunkSize = 2 };
            using IConnectSource tunnel = await source.GetConnectSourceAsync(Guid.NewGuid());
            await tunnel.ConnectAsync(new Uri($"tcp://{listener.LocalEndpoint}"));
            Stream stream = await tunnel.GetStreamAsync();
            Assert.IsInstanceOfType(stream, typeof(TlsHandshakeChunkingStream));

            await stream.WriteAsync(hello, 0, hello.Length);

            CollectionAssert.AreEqual(hello.Skip(5).ToArray(), Reassemble(SplitRecords(await server)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new LocalProxySource { TlsHandshakeChunkSize = -1 });
        }

        static IEnumerable<byte[]> SplitRecords(byte[] data)
        {
            for (int i = 0; i < data.Length;)
            {
                int length = 5 + ((data[i + 3] << 8) | data[i + 4]);
                yield return data.Skip(i).Take(length).ToArray();
                i += length;
            }
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
