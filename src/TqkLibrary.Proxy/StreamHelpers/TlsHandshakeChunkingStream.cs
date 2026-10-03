using System.Net.Sockets;
using System.Runtime.InteropServices;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Streams;

namespace TqkLibrary.Proxy.StreamHelpers
{
    /// <summary>
    /// Write-side decorator for a TLS connection: the handshake records this side sends (the
    /// ClientHello with its SNI first of all) go out <c>chunkSize</c> bytes per write, so a DPI box
    /// reading single TCP segments never sees the whole record. The first record that is not a
    /// handshake record (ChangeCipherSpec, application data, an alert) ends that, and every byte
    /// from there on is passed through untouched. A stream whose first byte is not a TLS handshake
    /// record is passed through from the start. Reads are never touched.
    /// </summary>
    /// <remarks>
    /// When a <see cref="Socket"/> is given, <see cref="Socket.NoDelay"/> is turned on for the
    /// handshake, which is what keeps the stack from coalescing the chunks back into one segment,
    /// and put back to what it was once the handshake is over. One writer at a time, as with any
    /// stream. Disposes <c>baseStream</c> unless told otherwise; never disposes the socket.
    /// </remarks>
    public class TlsHandshakeChunkingStream : BaseInheritStream, IHalfClosableStream
    {
        const byte HandshakeContentType = 0x16;
        const int RecordHeaderLength = 5;

        readonly int _chunkSize;
        readonly Socket? _socket;
        readonly bool _socketNoDelay;

        // Where the parser is in the record stream: the header bytes gathered so far, or how much
        // of the current handshake record's body is still to come.
        readonly byte[] _header = new byte[RecordHeaderLength];
        int _headerFilled;
        int _recordRemaining;
        bool _passthrough;

        public TlsHandshakeChunkingStream(Stream baseStream, int chunkSize, Socket? socket = null, bool disposeBaseStream = true)
            : base(baseStream, disposeBaseStream)
        {
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize));
            _chunkSize = chunkSize;
            _socket = socket;
            if (socket is not null)
            {
                _socketNoDelay = socket.NoDelay;
                socket.NoDelay = true;
            }
        }

        /// <summary>True once the handshake is over and writes go straight through.</summary>
        public bool IsPassthrough => _passthrough;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_passthrough)
            {
                _baseStream.Write(buffer, offset, count);
                return;
            }
            int handshake = TakeHandshakePrefix(buffer, offset, count);
            for (int i = 0; i < handshake; i += _chunkSize)
            {
                _baseStream.Write(buffer, offset + i, Math.Min(_chunkSize, handshake - i));
                _baseStream.Flush();
            }
            if (handshake < count)
                _baseStream.Write(buffer, offset + handshake, count - handshake);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_passthrough)
                return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            return WriteHandshakeAsync(buffer, offset, count, cancellationToken);
        }

#if NET5_0_OR_GREATER
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (_passthrough)
                _baseStream.Write(buffer);
            else
                Write(buffer.ToArray(), 0, buffer.Length);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_passthrough)
                return _baseStream.WriteAsync(buffer, cancellationToken);
            ArraySegment<byte> segment = MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array)
                ? array
                : new ArraySegment<byte>(buffer.ToArray());
            return new ValueTask(WriteHandshakeAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }
#endif

        async Task WriteHandshakeAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int handshake = TakeHandshakePrefix(buffer, offset, count);
            for (int i = 0; i < handshake; i += _chunkSize)
            {
                await _baseStream.WriteAsync(buffer, offset + i, Math.Min(_chunkSize, handshake - i), cancellationToken).ConfigureAwait(false);
                await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (handshake < count)
                await _baseStream.WriteAsync(buffer, offset + handshake, count - handshake, cancellationToken).ConfigureAwait(false);
        }

        // Walks the bytes about to be written and returns how many of the leading ones belong to
        // handshake records. Stops at the first byte that starts a record of any other type, and
        // from then on the stream is in passthrough. Only the content type byte is looked at: it is
        // all that tells a handshake record from the rest, and it is the first byte of the header,
        // so the decision never waits on bytes that have not been written yet.
        int TakeHandshakePrefix(byte[] buffer, int offset, int count)
        {
            int i = 0;
            while (i < count)
            {
                if (_recordRemaining > 0)
                {
                    int take = Math.Min(_recordRemaining, count - i);
                    _recordRemaining -= take;
                    i += take;
                    continue;
                }

                byte b = buffer[offset + i];
                if (_headerFilled == 0 && b != HandshakeContentType)
                {
                    EndHandshake();
                    return i;
                }
                _header[_headerFilled++] = b;
                i++;
                if (_headerFilled == RecordHeaderLength)
                {
                    _recordRemaining = (_header[3] << 8) | _header[4];
                    _headerFilled = 0;
                }
            }
            return i;
        }

        /// <summary>
        /// Half-closes the connection underneath: through the socket when one was given, else
        /// through the wrapped stream if it is a <see cref="NetworkStream"/> or can half-close
        /// itself. Without this the relay would see a decorator, not a NetworkStream, and never
        /// pass the FIN on.
        /// </summary>
        public void ShutdownSend()
        {
            if (_socket is not null)
                _socket.Shutdown(SocketShutdown.Send);
            else if (_baseStream is IHalfClosableStream halfClosable)
                halfClosable.ShutdownSend();
#if NET6_0_OR_GREATER
            else if (_baseStream is NetworkStream networkStream)
                networkStream.Socket.Shutdown(SocketShutdown.Send);
#endif
        }

        void EndHandshake()
        {
            _passthrough = true;
            if (_socket is null) return;
            try { _socket.NoDelay = _socketNoDelay; }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
    }
}
