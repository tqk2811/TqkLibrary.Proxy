using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Transport.RawTcp
{
    /// <summary>
    /// Single-port TCP transport. The first byte from the client identifies the channel kind:
    ///   0x01 = control, 0x02 = data.
    /// </summary>
    public sealed class RawTcpReverseTransportServer : IReverseTransportServer
    {
        public const byte ChannelControl = 0x01;
        public const byte ChannelData = 0x02;

        private readonly IPEndPoint _endpoint;
        private readonly RawTcpTlsOptions? _tls;
        private TcpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;
        private int _disposed;

        public event Func<IControlChannel, CancellationToken, Task>? ControlChannelAccepted;
        public event Func<Stream, CancellationToken, Task>? DataChannelAccepted;

        public RawTcpReverseTransportServer(IPEndPoint endpoint, RawTcpTlsOptions? tls = null)
        {
            _endpoint = endpoint;
            _tls = tls;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_listener is not null) return Task.CompletedTask;
            _listener = new TcpListener(_endpoint);
            _listener.Start();
            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (_listener is null) return;
            try { _cts?.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            if (_acceptLoop is not null)
            {
                try { await _acceptLoop.ConfigureAwait(false); } catch { }
            }
            _listener = null;
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            var listener = _listener!;
            while (!ct.IsCancellationRequested)
            {
                TcpClient tcp;
                try
                {
#if NET6_0_OR_GREATER
                    tcp = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
#else
                    tcp = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
#endif
                }
                catch (OperationCanceledException) { break; }
                catch (ObjectDisposedException) { break; }
                catch { continue; }

                _ = HandleAcceptedAsync(tcp, ct);
            }
        }

        private async Task HandleAcceptedAsync(TcpClient tcp, CancellationToken ct)
        {
            Stream stream = tcp.GetStream();
            string remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";
            try
            {
                if (_tls?.ServerCertificate is not null)
                {
                    var ssl = new SslStream(stream, false, _tls.ValidateClientCertificate);
#if NET6_0_OR_GREATER
                    var opts = new SslServerAuthenticationOptions
                    {
                        ServerCertificate = _tls.ServerCertificate,
                        ClientCertificateRequired = _tls.RequireClientCertificate,
                        EnabledSslProtocols = _tls.Protocols,
                        RemoteCertificateValidationCallback = _tls.ValidateClientCertificate,
                    };
                    await ssl.AuthenticateAsServerAsync(opts, ct).ConfigureAwait(false);
#else
                    await ssl.AuthenticateAsServerAsync(
                        _tls.ServerCertificate,
                        _tls.RequireClientCertificate,
                        _tls.Protocols,
                        false).ConfigureAwait(false);
#endif
                    stream = ssl;
                }

                var kindBuf = new byte[1];
#if NET6_0_OR_GREATER
                int n = await stream.ReadAsync(kindBuf.AsMemory(0, 1), ct).ConfigureAwait(false);
#else
                int n = await stream.ReadAsync(kindBuf, 0, 1, ct).ConfigureAwait(false);
#endif
                if (n != 1) { stream.Dispose(); tcp.Dispose(); return; }

                if (kindBuf[0] == ChannelControl)
                {
                    var channel = new StreamControlChannel(stream, remote);
                    var ev = ControlChannelAccepted;
                    if (ev is null) { await channel.DisposeAsync().ConfigureAwait(false); tcp.Dispose(); return; }
                    await ev(channel, ct).ConfigureAwait(false);
                }
                else if (kindBuf[0] == ChannelData)
                {
                    var ev = DataChannelAccepted;
                    if (ev is null) { stream.Dispose(); tcp.Dispose(); return; }
                    await ev(stream, ct).ConfigureAwait(false);
                }
                else
                {
                    stream.Dispose();
                    tcp.Dispose();
                }
            }
            catch
            {
                try { stream.Dispose(); } catch { }
                try { tcp.Dispose(); } catch { }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return default;
            try { _cts?.Cancel(); } catch { }
            try { _listener?.Stop(); } catch { }
            _cts?.Dispose();
            return default;
        }
    }
}
