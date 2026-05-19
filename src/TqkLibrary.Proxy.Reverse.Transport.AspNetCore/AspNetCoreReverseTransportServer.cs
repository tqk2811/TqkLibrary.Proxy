using System.Net.WebSockets;
using TqkLibrary.Proxy.Reverse.Transport;
using TqkLibrary.Proxy.Reverse.Transport.WebSockets;

namespace TqkLibrary.Proxy.Reverse.Transport.AspNetCore
{
    /// <summary>
    /// Transport hosted inside an existing ASP.NET Core app. The host owns Kestrel/lifecycle;
    /// this transport is fed WebSockets via <see cref="AcceptControlAsync"/> / <see cref="AcceptDataAsync"/>,
    /// typically through the <c>MapReverseProxy</c> endpoint extension.
    /// </summary>
    public sealed class AspNetCoreReverseTransportServer : IReverseTransportServer
    {
        public event Func<IControlChannel, CancellationToken, Task>? ControlChannelAccepted;
        public event Func<Stream, CancellationToken, Task>? DataChannelAccepted;

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => default;

        /// <summary>Hand a freshly-upgraded control WebSocket to the transport. Returns when the channel ends.</summary>
        public async Task AcceptControlAsync(WebSocket socket, string remoteEndPoint, CancellationToken cancellationToken)
        {
            var stream = new WebSocketStream(socket);
            var channel = new StreamControlChannel(stream, remoteEndPoint);
            var ev = ControlChannelAccepted;
            if (ev is null) { await channel.DisposeAsync().ConfigureAwait(false); return; }
            await ev(channel, cancellationToken).ConfigureAwait(false);
            // hold the request open until the socket closes
            await WaitClosedAsync(socket, cancellationToken).ConfigureAwait(false);
        }

        public async Task AcceptDataAsync(WebSocket socket, CancellationToken cancellationToken)
        {
            var stream = new WebSocketStream(socket);
            var ev = DataChannelAccepted;
            if (ev is null) { stream.Dispose(); return; }
            await ev(stream, cancellationToken).ConfigureAwait(false);
            await WaitClosedAsync(socket, cancellationToken).ConfigureAwait(false);
        }

        private static async Task WaitClosedAsync(WebSocket socket, CancellationToken ct)
        {
            try
            {
                while (socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    await Task.Delay(1000, ct).ConfigureAwait(false);
            }
            catch { }
        }
    }
}
