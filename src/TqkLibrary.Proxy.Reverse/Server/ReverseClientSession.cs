using System.Collections.Concurrent;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.Reverse.Protocol;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Server
{
    /// <summary>
    /// One client connection presented as an IProxySource.
    /// All proxy requests routed to this instance are forwarded to the remote client over its control channel,
    /// and outbound traffic flows over data channels the client opens back to the server.
    /// </summary>
    public sealed class ReverseClientSession : IProxySource, IAsyncDisposable
    {
        private readonly IControlChannel _control;
        private readonly ConcurrentDictionary<Guid, PendingTunnel> _pending = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private Task? _readLoop;
        private int _disposed;

        public Guid SessionId { get; }
        public string ClientId { get; }
        public IReadOnlyList<string> Tags { get; }
        public string RemoteEndPoint => _control.RemoteEndPoint;

        public bool IsSupportUdp { get; }
        public bool IsSupportIpv6 { get; }
        public bool IsSupportBind { get; }

        /// <summary>Raised when the underlying control channel ends (clean or error).</summary>
        public event Action<ReverseClientSession, Exception?>? Closed;

        internal ReverseClientSession(
            IControlChannel control,
            Guid sessionId,
            string clientId,
            IReadOnlyList<string> tags,
            bool supportUdp, bool supportBind, bool supportIpv6)
        {
            _control = control;
            SessionId = sessionId;
            ClientId = clientId;
            Tags = tags;
            IsSupportUdp = supportUdp;
            IsSupportBind = supportBind;
            IsSupportIpv6 = supportIpv6;
        }

        internal void Start()
        {
            _readLoop = Task.Run(() => ReadLoopAsync(_cts.Token));
        }

        // -------- IProxySource --------

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            return new ReverseConnectSource(this, tunnelId);
        }

        public async Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            if (!IsSupportBind) throw new NotSupportedException("Client does not advertise BIND support.");
            await Task.Yield();
            return new ReverseBindSource(this, tunnelId);
        }

        public async Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            if (!IsSupportUdp) throw new NotSupportedException("Client does not advertise UDP support.");
            var src = new ReverseUdpAssociateSource(this, tunnelId);
            await src.RequestAsync(cancellationToken).ConfigureAwait(false);
            return src;
        }

        // -------- internal: pending tunnels --------

        internal PendingTunnel CreatePending(Guid tunnelId, CancellationToken ct)
        {
            var pending = new PendingTunnel();
            if (!_pending.TryAdd(tunnelId, pending))
                throw new InvalidOperationException($"Duplicate tunnelId {tunnelId}");
            pending.CtReg = ct.Register(() =>
            {
                if (_pending.TryRemove(tunnelId, out var p))
                    p.Fail(new OperationCanceledException(ct));
            });
            return pending;
        }

        internal void RemovePending(Guid tunnelId)
        {
            if (_pending.TryRemove(tunnelId, out var p))
                p.CtReg.Dispose();
        }

        internal Task SendTunnelCloseSafeAsync(Guid tunnelId)
        {
            if (_disposed != 0) return Task.CompletedTask;
            return SendControlAsync(FrameType.TunnelClose, new TunnelClosePayload { TunnelId = tunnelId }, CancellationToken.None);
        }

        // -------- internal: control I/O --------

        internal async Task SendControlAsync<T>(FrameType type, T payload, CancellationToken ct)
        {
            var frame = FrameCodec.Encode(type, payload);
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _control.SendAsync(frame, ct).ConfigureAwait(false);
            }
            finally { _sendLock.Release(); }
        }

        // -------- internal: data stream routing --------

        /// <summary>
        /// Called by the server when an incoming data stream has a DataHello matching this session.
        /// Resolves the pending tunnel waiter.
        /// </summary>
        internal bool TryAcceptDataStream(Guid tunnelId, Stream stream)
        {
            if (_pending.TryGetValue(tunnelId, out var p))
            {
                if (p.DataStreamTcs.TrySetResult(stream))
                    return true;
            }
            return false;
        }

        // -------- read loop --------

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            Exception? failure = null;
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var frame = await _control.ReceiveAsync(ct).ConfigureAwait(false);
                    DispatchFrame(frame);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
            finally
            {
                FailAllPending(failure ?? new IOException("Control channel closed."));
                Closed?.Invoke(this, failure);
            }
        }

        private void DispatchFrame(ReverseFrame frame)
        {
            switch (frame.Type)
            {
                case FrameType.TunnelError:
                    {
                        var p = FrameCodec.Decode<TunnelErrorPayload>(frame);
                        if (_pending.TryGetValue(p.TunnelId, out var pt))
                            pt.Fail(new IOException($"Tunnel error [{p.Code}]: {p.Message}"));
                        break;
                    }
                case FrameType.BindReady:
                    {
                        var p = FrameCodec.Decode<BindReadyPayload>(frame);
                        if (_pending.TryGetValue(p.TunnelId, out var pt))
                            pt.BindReadyTcs.TrySetResult(p.ToEndPoint());
                        break;
                    }
                case FrameType.BindAccepted:
                    {
                        // informational; data stream arrival will resolve DataStreamTcs
                        break;
                    }
                case FrameType.TunnelClose:
                    {
                        var p = FrameCodec.Decode<TunnelClosePayload>(frame);
                        if (_pending.TryRemove(p.TunnelId, out var pt))
                            pt.Fail(new IOException("Tunnel closed by remote."));
                        break;
                    }
                case FrameType.Ping:
                    _ = SendControlAsync(FrameType.Pong, new { }, CancellationToken.None);
                    break;
                case FrameType.Pong:
                    break;
                default:
                    // unknown / not expected on server side
                    break;
            }
        }

        private void FailAllPending(Exception ex)
        {
            foreach (var kvp in _pending)
            {
                kvp.Value.Fail(ex);
                kvp.Value.CtReg.Dispose();
            }
            _pending.Clear();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { _cts.Cancel(); } catch { }
            try { await _control.DisposeAsync().ConfigureAwait(false); } catch { }
            if (_readLoop is not null)
            {
                try { await _readLoop.ConfigureAwait(false); } catch { }
            }
            _cts.Dispose();
            _sendLock.Dispose();
        }
    }
}
