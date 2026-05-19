using TqkLibrary.Proxy.Reverse.Protocol;

namespace TqkLibrary.Proxy.Reverse.Transport
{
    /// <summary>
    /// Bidirectional, ordered, reliable frame channel between server and one client.
    /// Implementations: RawTcp (SslStream/NetworkStream), WebSocket, ASP.NET Core, ...
    /// All methods must be safe for one writer + one reader concurrently.
    /// SendAsync may be called concurrently from multiple producers; implementations must serialize internally.
    /// </summary>
    public interface IControlChannel : IAsyncDisposable
    {
        /// <summary>Remote endpoint description (e.g. "1.2.3.4:5678"), for diagnostics.</summary>
        string RemoteEndPoint { get; }

        Task SendAsync(ReverseFrame frame, CancellationToken cancellationToken = default);

        Task<ReverseFrame> ReceiveAsync(CancellationToken cancellationToken = default);
    }
}
