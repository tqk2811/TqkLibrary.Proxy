using System.Collections.Concurrent;
using TqkLibrary.Proxy.Reverse.Protocol;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Server
{
    /// <summary>
    /// Hosts the reverse-proxy server side. Accepts client connections from the underlying transport,
    /// performs the Hello handshake, and fires <see cref="ClientConnected"/> for each authenticated session.
    /// Each session is itself an <see cref="TqkLibrary.Proxy.Interfaces.IProxySource"/> that the consumer
    /// can plug into a <c>ProxyServer</c>.
    /// </summary>
    public sealed class ReverseServer : IAsyncDisposable
    {
        private readonly IReverseTransportServer _transport;
        private readonly ReverseServerOptions _options;
        private readonly ConcurrentDictionary<Guid, ReverseClientSession> _sessions = new();
        private int _disposed;

        public ReverseServer(IReverseTransportServer transport, ReverseServerOptions? options = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _options = options ?? new ReverseServerOptions();
            _transport.ControlChannelAccepted += OnControlChannelAcceptedAsync;
            _transport.DataChannelAccepted += OnDataChannelAcceptedAsync;
        }

        /// <summary>Active authenticated sessions. Snapshot.</summary>
        public IReadOnlyCollection<ReverseClientSession> Sessions => _sessions.Values.ToArray();

        public event Func<ReverseClientSession, Task>? ClientConnected;
        public event Func<ReverseClientSession, Exception?, Task>? ClientDisconnected;

        public Task StartAsync(CancellationToken cancellationToken = default)
            => _transport.StartAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken = default)
            => _transport.StopAsync(cancellationToken);

        // -------- transport callbacks --------

        private async Task OnControlChannelAcceptedAsync(IControlChannel channel, CancellationToken ct)
        {
            ReverseClientSession? session = null;
            try
            {
                using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hsCts.CancelAfter(_options.HandshakeTimeout);

                var helloFrame = await channel.ReceiveAsync(hsCts.Token).ConfigureAwait(false);
                if (helloFrame.Type != FrameType.Hello)
                    throw new InvalidDataException($"Expected Hello, got {helloFrame.Type}");

                var hello = FrameCodec.Decode<HelloPayload>(helloFrame);

                bool ok = _options.Authenticate is null
                    || await _options.Authenticate(hello, hsCts.Token).ConfigureAwait(false);
                if (!ok)
                {
                    await channel.SendAsync(FrameCodec.Encode(FrameType.HelloDeny,
                        new HelloDenyPayload { Reason = "Unauthorized" }), hsCts.Token).ConfigureAwait(false);
                    await channel.DisposeAsync().ConfigureAwait(false);
                    return;
                }

                var sessionId = Guid.NewGuid();
                await channel.SendAsync(FrameCodec.Encode(FrameType.HelloAck,
                    new HelloAckPayload { ServerId = Environment.MachineName, SessionId = sessionId }),
                    hsCts.Token).ConfigureAwait(false);

                session = new ReverseClientSession(
                    channel,
                    sessionId,
                    hello.ClientId ?? sessionId.ToString("N"),
                    hello.Tags ?? Array.Empty<string>(),
                    hello.SupportUdp, hello.SupportBind, hello.SupportIpv6);

                _sessions[sessionId] = session;
                session.Closed += OnSessionClosed;
                session.Start();

                if (_options.OnClientConnected is not null)
                    await _options.OnClientConnected(session, ct).ConfigureAwait(false);

                var ev = ClientConnected;
                if (ev is not null)
                    await ev(session).ConfigureAwait(false);
            }
            catch
            {
                if (session is not null)
                {
                    _sessions.TryRemove(session.SessionId, out _);
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    try { await channel.DisposeAsync().ConfigureAwait(false); } catch { }
                }
            }
        }

        private async Task OnDataChannelAcceptedAsync(Stream stream, CancellationToken ct)
        {
            try
            {
                using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hsCts.CancelAfter(_options.DataHelloTimeout);

                var frame = await FrameCodec.ReadAsync(stream, hsCts.Token).ConfigureAwait(false);
                if (frame.Type != FrameType.DataHello)
                    throw new InvalidDataException($"Expected DataHello, got {frame.Type}");

                var dh = FrameCodec.Decode<DataHelloPayload>(frame);
                if (!_sessions.TryGetValue(dh.SessionId, out var session))
                    throw new InvalidDataException($"Unknown SessionId {dh.SessionId}");

                if (!session.TryAcceptDataStream(dh.TunnelId, stream))
                    throw new InvalidDataException($"No pending tunnel {dh.TunnelId} on session {dh.SessionId}");
            }
            catch
            {
                try { stream.Dispose(); } catch { }
            }
        }

        private void OnSessionClosed(ReverseClientSession s, Exception? ex)
        {
            _sessions.TryRemove(s.SessionId, out _);
            try { _options.OnClientDisconnected?.Invoke(s, ex); } catch { }
            try { ClientDisconnected?.Invoke(s, ex); } catch { }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { await _transport.StopAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
            foreach (var s in _sessions.Values)
            {
                try { await s.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            _sessions.Clear();
            try { await _transport.DisposeAsync().ConfigureAwait(false); } catch { }
        }
    }
}
