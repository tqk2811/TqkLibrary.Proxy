using TqkLibrary.Proxy.Reverse.Protocol;

namespace TqkLibrary.Proxy.Reverse.Transport
{
    /// <summary>Generic IControlChannel wrapping any duplex Stream (NetworkStream / SslStream / WebSocket-stream / ...).</summary>
    public sealed class StreamControlChannel : IControlChannel
    {
        private readonly Stream _stream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private int _disposed;

        public string RemoteEndPoint { get; }

        public StreamControlChannel(Stream stream, string remoteEndPoint)
        {
            _stream = stream;
            RemoteEndPoint = remoteEndPoint;
        }

        public async Task SendAsync(ReverseFrame frame, CancellationToken cancellationToken = default)
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await FrameCodec.WriteAsync(_stream, frame, cancellationToken).ConfigureAwait(false); }
            finally { _writeLock.Release(); }
        }

        public Task<ReverseFrame> ReceiveAsync(CancellationToken cancellationToken = default)
            => FrameCodec.ReadAsync(_stream, cancellationToken);

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return default;
            try { _stream.Dispose(); } catch { }
            _writeLock.Dispose();
            return default;
        }
    }
}
