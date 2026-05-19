using TqkLibrary.Proxy.Reverse.Protocol;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Client
{
    /// <summary>
    /// Reverse-proxy client. Maintains the control channel to the server, dials outbound connections on demand,
    /// and opens fresh data streams back to the server for each tunnel.
    /// </summary>
    public sealed class ReverseClient
    {
        private readonly IReverseTransportClient _transport;
        private readonly ReverseClientHandler _handler;
        private readonly ReverseClientOptions _options;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        private IControlChannel? _control;
        private Guid _sessionId;

        public ReverseClient(IReverseTransportClient transport, ReverseClientHandler? handler = null, ReverseClientOptions? options = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _handler = handler ?? new ReverseClientHandler();
            _options = options ?? new ReverseClientOptions();
        }

        /// <summary>Run until cancelled. Reconnects automatically if AutoReconnect is true.</summary>
        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                Exception? failure = null;
                try
                {
                    attempt++;
                    await OneSessionAsync(cancellationToken).ConfigureAwait(false);
                    attempt = 0;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { failure = ex; }

                try { await _handler.OnDisconnectedAsync(failure).ConfigureAwait(false); } catch { }
                if (!_options.AutoReconnect)
                {
                    if (failure is not null) throw failure;
                    return;
                }
                var delay = _handler.NextReconnectDelay(attempt);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task OneSessionAsync(CancellationToken ct)
        {
            _control = await _transport.ConnectControlAsync(ct).ConfigureAwait(false);
            try
            {
                using var hsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                hsCts.CancelAfter(_options.HandshakeTimeout);

                await _control.SendAsync(FrameCodec.Encode(FrameType.Hello, new HelloPayload
                {
                    ClientId = _options.ClientId,
                    Token = _options.Token,
                    Tags = _options.Tags,
                    SupportUdp = _options.SupportUdp,
                    SupportBind = _options.SupportBind,
                    SupportIpv6 = _options.SupportIpv6,
                }), hsCts.Token).ConfigureAwait(false);

                var ack = await _control.ReceiveAsync(hsCts.Token).ConfigureAwait(false);
                if (ack.Type == FrameType.HelloDeny)
                {
                    var deny = FrameCodec.Decode<HelloDenyPayload>(ack);
                    throw new UnauthorizedAccessException($"Server denied: {deny.Reason}");
                }
                if (ack.Type != FrameType.HelloAck)
                    throw new InvalidDataException($"Expected HelloAck, got {ack.Type}");
                _sessionId = FrameCodec.Decode<HelloAckPayload>(ack).SessionId;

                await _handler.OnConnectedAsync(ct).ConfigureAwait(false);

                using var keepAliveCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var keepAlive = _options.KeepAliveInterval > TimeSpan.Zero
                    ? Task.Run(() => KeepAliveLoopAsync(keepAliveCts.Token))
                    : Task.CompletedTask;

                try
                {
                    await ControlReadLoopAsync(ct).ConfigureAwait(false);
                }
                finally
                {
                    keepAliveCts.Cancel();
                    try { await keepAlive.ConfigureAwait(false); } catch { }
                }
            }
            finally
            {
                try { await _control.DisposeAsync().ConfigureAwait(false); } catch { }
                _control = null;
            }
        }

        private async Task ControlReadLoopAsync(CancellationToken ct)
        {
            var ctrl = _control!;
            while (!ct.IsCancellationRequested)
            {
                var frame = await ctrl.ReceiveAsync(ct).ConfigureAwait(false);
                _ = HandleFrameAsync(frame, ct); // fire-and-forget per-tunnel work
            }
        }

        private async Task HandleFrameAsync(ReverseFrame frame, CancellationToken ct)
        {
            try
            {
                switch (frame.Type)
                {
                    case FrameType.OpenConnect:
                        {
                            var p = FrameCodec.Decode<OpenConnectPayload>(frame);
                            await HandleOpenConnectAsync(p, ct).ConfigureAwait(false);
                            break;
                        }
                    case FrameType.OpenBind:
                        {
                            var p = FrameCodec.Decode<OpenBindPayload>(frame);
                            await HandleOpenBindAsync(p, ct).ConfigureAwait(false);
                            break;
                        }
                    case FrameType.OpenUdp:
                        {
                            var p = FrameCodec.Decode<OpenUdpPayload>(frame);
                            await HandleOpenUdpAsync(p, ct).ConfigureAwait(false);
                            break;
                        }
                    case FrameType.Ping:
                        await SendControlAsync(FrameType.Pong, new { }, ct).ConfigureAwait(false);
                        break;
                    case FrameType.Pong:
                    case FrameType.TunnelClose:
                        break;
                }
            }
            catch
            {
                // swallow per-frame errors; control loop continues
            }
        }

        private async Task HandleOpenConnectAsync(OpenConnectPayload p, CancellationToken ct)
        {
            if (!await _handler.OnOpenConnectAsync(p.TunnelId, p.Host, p.Port, ct).ConfigureAwait(false))
            {
                await SendTunnelErrorAsync(p.TunnelId, "Rejected", "Filtered by handler", ct).ConfigureAwait(false);
                return;
            }

            Stream? target = null;
            Stream? data = null;
            try
            {
                target = await _handler.DialConnectAsync(p.Host, p.Port, ct).ConfigureAwait(false);
                data = await OpenDataChannelAsync(p.TunnelId, ct).ConfigureAwait(false);
                _ = StreamCopy.PumpAsync(target, data, ct); // fire-and-forget bridge
            }
            catch (Exception ex)
            {
                target?.Dispose();
                data?.Dispose();
                await SendTunnelErrorAsync(p.TunnelId, "DialFailed", ex.Message, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task HandleOpenBindAsync(OpenBindPayload p, CancellationToken ct)
        {
            try
            {
                var (ep, accept) = await _handler.DialBindAsync(ct).ConfigureAwait(false);
                await SendControlAsync(FrameType.BindReady, new BindReadyPayload
                {
                    TunnelId = p.TunnelId,
                    Address = ep.Address.ToString(),
                    Port = ep.Port,
                }, ct).ConfigureAwait(false);

                var inbound = await accept(ct).ConfigureAwait(false);
                var data = await OpenDataChannelAsync(p.TunnelId, ct).ConfigureAwait(false);
                await SendControlAsync(FrameType.BindAccepted, new BindAcceptedPayload
                {
                    TunnelId = p.TunnelId,
                }, ct).ConfigureAwait(false);
                _ = StreamCopy.PumpAsync(inbound, data, ct);
            }
            catch (Exception ex)
            {
                await SendTunnelErrorAsync(p.TunnelId, "BindFailed", ex.Message, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task HandleOpenUdpAsync(OpenUdpPayload p, CancellationToken ct)
        {
            try
            {
                var udp = await _handler.DialUdpAsync(ct).ConfigureAwait(false);
                var data = await OpenDataChannelAsync(p.TunnelId, ct).ConfigureAwait(false);
                _ = StreamCopy.PumpAsync(udp, data, ct);
            }
            catch (Exception ex)
            {
                await SendTunnelErrorAsync(p.TunnelId, "UdpFailed", ex.Message, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task<Stream> OpenDataChannelAsync(Guid tunnelId, CancellationToken ct)
        {
            var s = await _transport.OpenDataAsync(ct).ConfigureAwait(false);
            try
            {
                await FrameCodec.WriteAsync(s, FrameCodec.Encode(FrameType.DataHello, new DataHelloPayload
                {
                    TunnelId = tunnelId,
                    SessionId = _sessionId,
                }), ct).ConfigureAwait(false);
                return s;
            }
            catch
            {
                s.Dispose();
                throw;
            }
        }

        private Task SendTunnelErrorAsync(Guid id, string code, string msg, CancellationToken ct)
            => SendControlAsync(FrameType.TunnelError, new TunnelErrorPayload
            {
                TunnelId = id,
                Code = code,
                Message = msg,
            }, ct);

        private async Task SendControlAsync<T>(FrameType type, T payload, CancellationToken ct)
        {
            var ctrl = _control;
            if (ctrl is null) return;
            var frame = FrameCodec.Encode(type, payload);
            await _sendLock.WaitAsync(ct).ConfigureAwait(false);
            try { await ctrl.SendAsync(frame, ct).ConfigureAwait(false); }
            finally { _sendLock.Release(); }
        }

        private async Task KeepAliveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(_options.KeepAliveInterval, ct).ConfigureAwait(false);
                    await SendControlAsync(FrameType.Ping, new { }, ct).ConfigureAwait(false);
                }
            }
            catch { }
        }
    }
}
