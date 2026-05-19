using System.Net;
using System.Net.Sockets;

namespace TqkLibrary.Proxy.Reverse.Client
{
    /// <summary>
    /// User-overridable behavior for the reverse client. Default implementation:
    ///  - DialConnect: TCP connect to host:port from the client's local network.
    ///  - DialBind:    listen on a random TCP port and accept one connection.
    ///  - DialUdp:     not implemented (override).
    ///  - Reconnect:   exponential backoff 1s -> 30s.
    /// </summary>
    public class ReverseClientHandler
    {
        /// <summary>Open an outbound TCP stream to (host, port). Default: TcpClient.</summary>
        public virtual async Task<Stream> DialConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            var tcp = new TcpClient { NoDelay = true };
            try
            {
#if NET6_0_OR_GREATER
                await tcp.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
#else
                using (cancellationToken.Register(() => tcp.Dispose()))
                    await tcp.ConnectAsync(host, port).ConfigureAwait(false);
#endif
                return tcp.GetStream();
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Bind a local TCP listener and return its endpoint plus an accept-callback that yields the inbound stream.
        /// Default: random IPv4 port on Any.
        /// </summary>
        public virtual Task<(IPEndPoint endpoint, Func<CancellationToken, Task<Stream>> accept)> DialBindAsync(CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start(1);
            var ep = (IPEndPoint)listener.LocalEndpoint;

            Func<CancellationToken, Task<Stream>> accept = async ct =>
            {
#if NET6_0_OR_GREATER
                try
                {
                    var c = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    return (Stream)c.GetStream();
                }
                finally { listener.Stop(); }
#else
                using (ct.Register(() => listener.Stop()))
                {
                    try
                    {
                        var c = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                        return (Stream)c.GetStream();
                    }
                    finally { listener.Stop(); }
                }
#endif
            };
            return Task.FromResult((ep, accept));
        }

        /// <summary>Open a stream representing a UDP-over-stream tunnel. Override to implement.</summary>
        public virtual Task<Stream> DialUdpAsync(CancellationToken cancellationToken)
            => throw new NotSupportedException("UDP not supported. Override DialUdpAsync to enable.");

        /// <summary>Filter incoming OpenConnect requests. Return false to reject.</summary>
        public virtual Task<bool> OnOpenConnectAsync(Guid tunnelId, string host, int port, CancellationToken cancellationToken)
            => Task.FromResult(true);

        /// <summary>Reconnect backoff. attempt is 1-based.</summary>
        public virtual TimeSpan NextReconnectDelay(int attempt)
        {
            var s = Math.Min(30, Math.Pow(2, Math.Min(attempt, 5)));
            return TimeSpan.FromSeconds(s);
        }

        public virtual Task OnConnectedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public virtual Task OnDisconnectedAsync(Exception? exception) => Task.CompletedTask;
    }
}
