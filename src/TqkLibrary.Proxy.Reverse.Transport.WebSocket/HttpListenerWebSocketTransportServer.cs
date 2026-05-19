using System.Net;
using System.Net.WebSockets;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Transport.WebSockets
{
    /// <summary>
    /// Standalone WebSocket transport using <see cref="HttpListener"/>. Two paths:
    ///   /control = control channel, /data = data channel.
    /// For ASP.NET Core integration use TqkLibrary.Proxy.Reverse.Transport.AspNetCore instead.
    /// </summary>
    public sealed class HttpListenerWebSocketTransportServer : IReverseTransportServer
    {
        private readonly HttpListener _listener = new();
        private readonly string _controlPath;
        private readonly string _dataPath;
        private CancellationTokenSource? _cts;
        private Task? _acceptLoop;
        private int _disposed;

        public event Func<IControlChannel, CancellationToken, Task>? ControlChannelAccepted;
        public event Func<Stream, CancellationToken, Task>? DataChannelAccepted;

        /// <param name="prefix">e.g. "http://+:9000/"</param>
        public HttpListenerWebSocketTransportServer(string prefix,
            string controlPath = WebSocketTransportPaths.ControlPath,
            string dataPath = WebSocketTransportPaths.DataPath)
        {
            _listener.Prefixes.Add(prefix);
            _controlPath = controlPath;
            _dataPath = dataPath;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (_listener.IsListening) return Task.CompletedTask;
            _listener.Start();
            _cts = new CancellationTokenSource();
            _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            try { _cts?.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            if (_acceptLoop is not null)
            {
                try { await _acceptLoop.ConfigureAwait(false); } catch { }
            }
        }

        private async Task AcceptLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
                catch { break; }

                _ = HandleAsync(ctx, ct);
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx, CancellationToken ct)
        {
            try
            {
                if (!ctx.Request.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    return;
                }

                var path = ctx.Request.Url?.AbsolutePath ?? "";
                bool isControl = string.Equals(path, _controlPath, StringComparison.OrdinalIgnoreCase);
                bool isData = string.Equals(path, _dataPath, StringComparison.OrdinalIgnoreCase);
                if (!isControl && !isData)
                {
                    ctx.Response.StatusCode = 404;
                    ctx.Response.Close();
                    return;
                }

                var wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
                var ws = wsCtx.WebSocket;
                var remote = ctx.Request.RemoteEndPoint?.ToString() ?? "?";
                var stream = new WebSocketStream(ws);

                if (isControl)
                {
                    var ev = ControlChannelAccepted;
                    if (ev is null) { stream.Dispose(); return; }
                    await ev(new StreamControlChannel(stream, remote), ct).ConfigureAwait(false);
                }
                else
                {
                    var ev = DataChannelAccepted;
                    if (ev is null) { stream.Dispose(); return; }
                    await ev(stream, ct).ConfigureAwait(false);
                }
            }
            catch
            {
                try { ctx.Response.Abort(); } catch { }
            }
        }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return default;
            try { _cts?.Cancel(); } catch { }
            try { _listener.Close(); } catch { }
            _cts?.Dispose();
            return default;
        }
    }
}
