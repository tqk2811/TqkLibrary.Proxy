using System.Net.WebSockets;

namespace TqkLibrary.Proxy.Reverse.Transport.WebSockets
{
    /// <summary>
    /// Adapts a <see cref="WebSocket"/> to a bidirectional <see cref="Stream"/>.
    /// Writes are sent as a single binary message each. Reads pull bytes across messages.
    /// </summary>
    public sealed class WebSocketStream : Stream
    {
        private readonly WebSocket _ws;
        private readonly bool _ownsSocket;
        private byte[] _readBuf = Array.Empty<byte>();
        private int _readPos;
        private int _readLen;

        public WebSocketStream(WebSocket ws, bool ownsSocket = true)
        {
            _ws = ws;
            _ownsSocket = ownsSocket;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_readPos >= _readLen)
            {
                if (_readBuf.Length < 8192) _readBuf = new byte[8192];
                var seg = new ArraySegment<byte>(_readBuf, 0, _readBuf.Length);
                var result = await _ws.ReceiveAsync(seg, cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return 0;
                _readPos = 0;
                _readLen = result.Count;
                if (_readLen == 0) return 0;
            }
            int take = Math.Min(count, _readLen - _readPos);
            Buffer.BlockCopy(_readBuf, _readPos, buffer, offset, take);
            _readPos += take;
            return take;
        }

#if NET6_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_readPos >= _readLen)
            {
                if (_readBuf.Length < 8192) _readBuf = new byte[8192];
                var result = await _ws.ReceiveAsync(_readBuf.AsMemory(0, _readBuf.Length), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    return 0;
                _readPos = 0;
                _readLen = result.Count;
                if (_readLen == 0) return 0;
            }
            int take = Math.Min(buffer.Length, _readLen - _readPos);
            new ReadOnlySpan<byte>(_readBuf, _readPos, take).CopyTo(buffer.Span);
            _readPos += take;
            return take;
        }
#endif

        public override void Write(byte[] buffer, int offset, int count)
            => WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _ws.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, true, cancellationToken);

#if NET6_0_OR_GREATER
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => _ws.SendAsync(buffer, WebSocketMessageType.Binary, true, cancellationToken);
#endif

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                        _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None).GetAwaiter().GetResult();
                }
                catch { }
                if (_ownsSocket)
                    try { _ws.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
