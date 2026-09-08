using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TestProxy.ServerTest
{
    /// <summary>
    /// What the HTTP proxy server passes on, checked against an origin server of our own on
    /// loopback rather than against the internet.
    /// </summary>
    /// <remarks>
    /// Every case here was broken, and each of them silently: a request line without its query
    /// string, a chunked response whose body never arrived, a keep-alive connection closed after
    /// one request. None of it throws — the client simply gets the wrong page, or an empty one.
    /// </remarks>
    [TestClass]
    public class HttpProxyServerForwardingTest
    {
        ProxyServer? _proxy;
        FakeOrigin? _origin;

        [TestInitialize]
        public void Start()
        {
            _origin = new FakeOrigin();
            // The default handler refuses loopback destinations, which is the right answer for a
            // proxy on a network and the wrong one for an origin server this test just started.
            _proxy = new ProxyServer(IPEndPoint.Parse("127.0.0.1:0"), new LoopbackAllowingHandler());
            _proxy.StartListen();
        }

        [TestCleanup]
        public void Stop()
        {
            _proxy?.Dispose();
            _origin?.Dispose();
        }

        [TestMethod]
        public async Task The_query_string_reaches_the_origin()
        {
            _origin!.Reply = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";

            string response = await SendAsync("GET http://" + _origin.Host + "/search?q=proxy&page=2 HTTP/1.1");

            string requestLine = await _origin.FirstRequestLine.WaitAsync(TimeSpan.FromSeconds(3));
            StringAssert.Contains(requestLine, "/search?q=proxy&page=2", "proxy said: " + response);
        }

        [TestMethod]
        public async Task A_chunked_response_reaches_the_client_with_its_body()
        {
            _origin!.Reply =
                "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n"
                + "5\r\nhello\r\n"
                + "6\r\n world\r\n"
                + "0\r\n\r\n";

            string response = await SendAsync("GET http://" + _origin.Host + "/stream HTTP/1.1");

            StringAssert.Contains(response, "hello");
            StringAssert.Contains(response, " world");
        }

        [TestMethod]
        public async Task A_response_delimited_by_the_close_reaches_the_client()
        {
            // No Content-Length and no chunking: the body ends when the connection does, which is
            // what an HTTP/1.0 origin sends.
            _origin!.Reply = "HTTP/1.1 200 OK\r\nConnection: close\r\n\r\nthe whole body";
            _origin.CloseAfterReply = true;

            string response = await SendAsync("GET http://" + _origin.Host + "/old HTTP/1.1");

            StringAssert.Contains(response, "the whole body");
        }

        [TestMethod]
        public async Task A_second_request_can_use_the_same_connection()
        {
            _origin!.Reply = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nConnection: keep-alive\r\n\r\nfirst";

            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _proxy!.IPEndPoint!.Port);
            using NetworkStream stream = client.GetStream();

            // Proxy-Connection is what this server reads to decide whether to keep the connection;
            // without it one request per connection is the intended behaviour, not the bug.
            string keepAlive = $"Host: {_origin.Host}\r\nProxy-Connection: keep-alive\r\n\r\n";

            string first = await ExchangeAsync(stream, $"GET http://{_origin.Host}/one HTTP/1.1\r\n{keepAlive}");
            StringAssert.Contains(first, "first");

            _origin.Reply = "HTTP/1.1 200 OK\r\nContent-Length: 6\r\nConnection: keep-alive\r\n\r\nsecond";
            string second = await ExchangeAsync(stream, $"GET http://{_origin.Host}/two HTTP/1.1\r\n{keepAlive}");

            // The proxy used to dispose the client's stream after the first response, so this read
            // came back empty however well-behaved the client was.
            StringAssert.Contains(second, "second");
        }

        async Task<string> SendAsync(string requestLine)
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, _proxy!.IPEndPoint!.Port);
            using NetworkStream stream = client.GetStream();

            return await ExchangeAsync(stream, $"{requestLine}\r\nHost: {_origin!.Host}\r\n\r\n");
        }

        static async Task<string> ExchangeAsync(NetworkStream stream, string request)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(bytes, 0, bytes.Length);
            await stream.FlushAsync();

            // Reads what arrives within a moment: several of these responses end only when the far
            // side closes, and one of them deliberately does not close at all.
            var received = new MemoryStream();
            byte[] buffer = new byte[4096];
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            try
            {
                while (true)
                {
                    int read = await stream.ReadAsync(buffer, 0, buffer.Length, deadline.Token);
                    if (read <= 0) break;
                    received.Write(buffer, 0, read);

                    string soFar = Encoding.ASCII.GetString(received.ToArray());
                    if (IsComplete(soFar)) break;
                }
            }
            catch (OperationCanceledException) { }

            return Encoding.ASCII.GetString(received.ToArray());
        }

        // Enough of an answer to stop reading: the headers plus whatever the framing says follows.
        static bool IsComplete(string response)
        {
            int headerEnd = response.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (headerEnd < 0) return false;

            string headers = response.Substring(0, headerEnd);
            string body = response.Substring(headerEnd + 4);

            if (headers.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0)
                return body.EndsWith("0\r\n\r\n", StringComparison.Ordinal);

            int at = headers.IndexOf("Content-Length:", StringComparison.OrdinalIgnoreCase);
            if (at < 0) return false;   // close-delimited: read until the socket ends

            string value = headers.Substring(at + "Content-Length:".Length).Split('\r')[0].Trim();
            return int.TryParse(value, out int length) && body.Length >= length;
        }

        sealed class LoopbackAllowingHandler : BaseProxyServerHandler
        {
            public LoopbackAllowingHandler() : base(new LocalProxySource()) { }

            public override Task<bool> IsAcceptDomainAsync(Uri uri, IUserInfo userInfo, CancellationToken cancellationToken = default)
                => Task.FromResult(true);
        }

        /// <summary>An origin server that answers every request with whatever the test set.</summary>
        sealed class FakeOrigin : IDisposable
        {
            readonly TcpListener _listener;
            readonly CancellationTokenSource _cts = new();

            public string Reply { get; set; } = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n";
            public bool CloseAfterReply { get; set; }
            // Completed by the accept thread, awaited by the test: the request line is what several
            // of these assertions are about, and reading a field across threads is a race the test
            // would lose intermittently.
            readonly TaskCompletionSource<string> _firstRequestLine =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<string> FirstRequestLine => _firstRequestLine.Task;
            public string Host => $"127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}";

            public FakeOrigin()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                _ = Task.Run(AcceptAsync);
            }

            async Task AcceptAsync()
            {
                while (!_cts.IsCancellationRequested)
                {
                    TcpClient client;
                    try { client = await _listener.AcceptTcpClientAsync(); }
                    catch { return; }

                    _ = Task.Run(() => ServeAsync(client));
                }
            }

            async Task ServeAsync(TcpClient client)
            {
                try
                {
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                    {
                        var buffer = new byte[8192];
                        while (!_cts.IsCancellationRequested)
                        {
                            int read = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                            if (read <= 0) return;

                            string request = Encoding.ASCII.GetString(buffer, 0, read);
                            _firstRequestLine.TrySetResult(request.Split('\r')[0]);

                            byte[] reply = Encoding.ASCII.GetBytes(Reply);
                            await stream.WriteAsync(reply, 0, reply.Length, _cts.Token);
                            await stream.FlushAsync(_cts.Token);

                            if (CloseAfterReply)
                            {
                                // A server that announces Connection: close still lets what it
                                // wrote drain before it goes. Closing the instant the write returns
                                // resets the connection instead, and the proxy reading the response
                                // headers gets an aborted socket rather than a body.
                                await Task.Delay(400, _cts.Token);
                                return;
                            }
                        }
                    }
                }
                catch { /* the test is finished with this connection */ }
            }

            public void Dispose()
            {
                try { _cts.Cancel(); } catch { }
                try { _listener.Stop(); } catch { }
                _cts.Dispose();
            }
        }
    }
}
