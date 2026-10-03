using System.Net.Sockets;
using System.Runtime.InteropServices;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Streams;

namespace TqkLibrary.Proxy.StreamHelpers
{
    /// <summary>
    /// Write-side decorator for a TLS connection that hides the server name of the ClientHello
    /// from a DPI box: the first record, when it is a ClientHello carrying an SNI, is re-framed into
    /// several TLS records — everything before the host name in one, the host name
    /// <c>chunkSize</c> bytes per record, everything after it in one — and each record goes out
    /// in its own write. A server has to put a handshake message split over records back together
    /// (RFC 8446 §5.1), so the bytes it reads are the same handshake; a DPI box that reads one
    /// record, or one segment, never sees the whole name. Every byte after that first record is
    /// passed through untouched, and so is a stream whose first record is anything else. Reads
    /// are never touched.
    /// </summary>
    /// <remarks>
    /// The first record is held back until it is complete, which costs nothing in practice: a TLS
    /// client writes its whole ClientHello before it waits for an answer. When a
    /// <see cref="Socket"/> is given, <see cref="Socket.NoDelay"/> is turned on so the stack does
    /// not coalesce the records back into one segment, and put back to what it was once the
    /// ClientHello is out. One writer at a time, as with any stream. Disposes <c>baseStream</c>
    /// unless told otherwise; never disposes the socket.
    /// </remarks>
    public class TlsHandshakeChunkingStream : BaseInheritStream, IHalfClosableStream
    {
        const byte HandshakeContentType = 0x16;
        const byte ClientHelloType = 0x01;
        const int RecordHeaderLength = 5;
        // 2^14 of plaintext plus what RFC 8446 §5.2 allows a record to grow by.
        const int MaxRecordBodyLength = 16384 + 256;

        readonly int _chunkSize;
        readonly Socket? _socket;
        readonly bool _socketNoDelay;

        // The first record, gathered over as many writes as it takes.
        readonly List<byte> _firstRecord = new List<byte>();
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

        /// <summary>True once the first record is out and writes go straight through.</summary>
        public bool IsPassthrough => _passthrough;

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_passthrough)
            {
                _baseStream.Write(buffer, offset, count);
                return;
            }
            List<byte[]> records = TakeFirstRecord(buffer, ref offset, ref count);
            foreach (byte[] record in records)
            {
                _baseStream.Write(record, 0, record.Length);
                _baseStream.Flush();
            }
            if (_passthrough) RestoreNoDelay();
            if (count > 0)
                _baseStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_passthrough)
                return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            return WriteFirstRecordAsync(buffer, offset, count, cancellationToken);
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
            return new ValueTask(WriteFirstRecordAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }
#endif

        async Task WriteFirstRecordAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            List<byte[]> records = TakeFirstRecord(buffer, ref offset, ref count);
            foreach (byte[] record in records)
            {
                await _baseStream.WriteAsync(record, 0, record.Length, cancellationToken).ConfigureAwait(false);
                await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            if (_passthrough) RestoreNoDelay();
            if (count > 0)
                await _baseStream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
        }

        // Moves the bytes of the first record out of the write into _firstRecord and advances
        // offset/count past them, leaving what follows the record for passthrough. Returns the
        // records to send, empty while the first record is still incomplete. Once the first record
        // is decided — complete, or not a handshake record at all — the stream is in passthrough.
        List<byte[]> TakeFirstRecord(byte[] buffer, ref int offset, ref int count)
        {
            if (_firstRecord.Count == 0 && count > 0 && buffer[offset] != HandshakeContentType)
            {
                _passthrough = true;
                return new List<byte[]>();
            }

            int need = RecordHeaderLength - _firstRecord.Count;
            if (need > 0)
            {
                int take = Math.Min(need, count);
                Take(buffer, ref offset, ref count, take);
                if (_firstRecord.Count < RecordHeaderLength)
                    return new List<byte[]>();
            }
            int bodyLength = (_firstRecord[3] << 8) | _firstRecord[4];
            if (_firstRecord[1] != 0x03 || bodyLength > MaxRecordBodyLength)
            {
                // Starts with 0x16 but is no TLS record header: something else that happens to
                // begin with that byte. Holding it for up to 64K would stall a protocol that waits
                // for the server to speak, so what was held goes out first and the rest follows.
                byte[] held = _firstRecord.ToArray();
                _firstRecord.Clear();
                _passthrough = true;
                return new List<byte[]> { held };
            }
            int recordLength = RecordHeaderLength + bodyLength;
            Take(buffer, ref offset, ref count, Math.Min(recordLength - _firstRecord.Count, count));
            if (_firstRecord.Count < recordLength)
                return new List<byte[]>();

            byte[] record = _firstRecord.ToArray();
            _firstRecord.Clear();
            _passthrough = true;
            return SplitAroundServerName(record, _chunkSize);
        }

        void Take(byte[] buffer, ref int offset, ref int count, int take)
        {
            for (int i = 0; i < take; i++)
                _firstRecord.Add(buffer[offset + i]);
            offset += take;
            count -= take;
        }

        /// <summary>
        /// Re-frames one handshake record into several: the body before the SNI host name, the
        /// host name in pieces of <paramref name="chunkSize"/> bytes, and the body after it, each
        /// under a header of its own carrying the original record's type and version. A record
        /// that is not a ClientHello, or has no host name to be found, comes back as it was.
        /// </summary>
        public static List<byte[]> SplitAroundServerName(byte[] record, int chunkSize)
        {
            byte[] body = new byte[record.Length - RecordHeaderLength];
            Buffer.BlockCopy(record, RecordHeaderLength, body, 0, body.Length);
            if (!TryFindServerName(body, out int nameStart, out int nameLength))
                return new List<byte[]> { record };

            List<byte[]> records = new List<byte[]>();
            void Add(int start, int length)
            {
                if (length <= 0) return;
                byte[] piece = new byte[RecordHeaderLength + length];
                piece[0] = record[0];
                piece[1] = record[1];
                piece[2] = record[2];
                piece[3] = (byte)(length >> 8);
                piece[4] = (byte)length;
                Buffer.BlockCopy(body, start, piece, RecordHeaderLength, length);
                records.Add(piece);
            }

            Add(0, nameStart);
            for (int i = 0; i < nameLength; i += chunkSize)
                Add(nameStart + i, Math.Min(chunkSize, nameLength - i));
            Add(nameStart + nameLength, body.Length - nameStart - nameLength);
            return records;
        }

        /// <summary>
        /// Finds the host name of the server_name extension in a ClientHello handshake message
        /// (the record body, starting at its handshake type byte). Returns false on anything that
        /// does not parse, rather than guessing.
        /// </summary>
        public static bool TryFindServerName(byte[] body, out int nameStart, out int nameLength)
        {
            nameStart = nameLength = 0;
            if (body.Length < 4 || body[0] != ClientHelloType) return false;

            int i = 4;          // msg_type, length (3)
            i += 2 + 32;        // legacy_version, random
            if (i + 1 > body.Length) return false;
            i += 1 + body[i];   // legacy_session_id
            if (i + 2 > body.Length) return false;
            i += 2 + ((body[i] << 8) | body[i + 1]); // cipher_suites
            if (i + 1 > body.Length) return false;
            i += 1 + body[i];   // legacy_compression_methods
            if (i + 2 > body.Length) return false;
            int end = Math.Min(body.Length, i + 2 + ((body[i] << 8) | body[i + 1]));
            i += 2;

            while (i + 4 <= end)
            {
                int type = (body[i] << 8) | body[i + 1];
                int length = (body[i + 2] << 8) | body[i + 3];
                int data = i + 4;
                if (type == 0x0000)
                {
                    // server_name_list length (2), name_type (1), HostName length (2), HostName
                    if (data + 5 > end || body[data + 2] != 0x00) return false;
                    int hostLength = (body[data + 3] << 8) | body[data + 4];
                    if (hostLength == 0 || data + 5 + hostLength > end) return false;
                    nameStart = data + 5;
                    nameLength = hostLength;
                    return true;
                }
                i = data + length;
            }
            return false;
        }

        /// <summary>
        /// Half-closes the connection underneath: through the socket when one was given, else
        /// through the wrapped stream if it is a <see cref="NetworkStream"/> or can half-close
        /// itself. Without this the relay would see a decorator, not a NetworkStream, and never
        /// pass the FIN on.
        /// </summary>
        public void ShutdownSend()
        {
            FlushHeldBytes();
            if (_socket is not null)
                _socket.Shutdown(SocketShutdown.Send);
            else if (_baseStream is IHalfClosableStream halfClosable)
                halfClosable.ShutdownSend();
#if NET6_0_OR_GREATER
            else if (_baseStream is NetworkStream networkStream)
                networkStream.Socket.Shutdown(SocketShutdown.Send);
#endif
        }

        // BaseInheritStream overrides Close and does not come back through Dispose(bool), so
        // Close is where the held bytes are let go.
        public override void Close()
        {
            try { FlushHeldBytes(); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            base.Close();
        }

        // A first record cut short by the end of the stream goes out as it is, never dropped.
        void FlushHeldBytes()
        {
            if (_passthrough || _firstRecord.Count == 0) return;
            byte[] held = _firstRecord.ToArray();
            _firstRecord.Clear();
            _passthrough = true;
            _baseStream.Write(held, 0, held.Length);
            _baseStream.Flush();
            RestoreNoDelay();
        }

        void RestoreNoDelay()
        {
            if (_socket is null) return;
            try { _socket.NoDelay = _socketNoDelay; }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }
    }
}
