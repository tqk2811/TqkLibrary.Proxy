using System.Text;
using TqkLibrary.Streams;

namespace TqkLibrary.Proxy.StreamHelpers
{
    public class PreReadStream : BaseInheritStream
    {
        byte[]? _preReadBuffer;
        int _preReadOffset;

        int _preReadLength => _preReadBuffer is null ? 0 : _preReadBuffer.Length - _preReadOffset;

        public PreReadStream(Stream baseStream, bool disposeBaseStream = true) : base(baseStream, disposeBaseStream)
        {
        }

        public async Task<byte[]> PreReadAsync(int count, CancellationToken cancellationToken = default)
        {
            int buffered = _preReadLength;
            if (buffered < count)
            {
                int needRead = count - buffered;
                byte[] newData = new byte[needRead];

                // Deliberately ONE read. Looping until the buffer is full would hang on the
                // ordinary case: a client sends its request line and then waits for an answer, so
                // the bytes it has not sent are never coming. Callers are expected to ask again
                // rather than to read a short result as the end of the stream.
                int bytesRead = await _baseStream.ReadAsync(newData, 0, needRead, cancellationToken);

                byte[] merged = new byte[buffered + bytesRead];
                if (buffered > 0)
                    Array.Copy(_preReadBuffer!, _preReadOffset, merged, 0, buffered);
                Array.Copy(newData, 0, merged, buffered, bytesRead);
                _preReadBuffer = merged;
                _preReadOffset = 0;
            }

            int returnCount = Math.Min(count, _preReadLength);
            byte[] result = new byte[returnCount];
            if (returnCount > 0)
                Array.Copy(_preReadBuffer!, _preReadOffset, result, 0, returnCount);
            return result;
        }
        /// <summary>
        /// Reads bytes incrementally until a CRLF line terminator is found, buffering all
        /// consumed bytes so subsequent reads are not affected.
        /// </summary>
        /// <param name="maxLength">Http header size or url max length</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The line including the trailing CRLF.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> PreReadLineAsync(int maxLength = 32 * 1024, CancellationToken cancellationToken = default)
        {
            int searchFrom = 0;
            int count = Math.Min(64, maxLength);
            int previousLength = -1;
            while (true)
            {
                byte[] buffer = await PreReadAsync(count, cancellationToken).ConfigureAwait(false);

                int searchEnd = buffer.Length - 1;
                for (int i = searchFrom; i < searchEnd; i++)
                {
                    if (buffer[i] == '\r' && buffer[i + 1] == '\n')
                        return Encoding.ASCII.GetString(buffer, 0, i + 2);
                }

                // Getting back fewer bytes than asked for used to be read as the end of the stream.
                // It is not: PreReadAsync does one read, and one read returns whatever a single
                // segment carried. A request line split across two segments — perfectly ordinary —
                // was rejected as truncated. Only a pass that adds nothing at all means the peer
                // has finished and the line is never coming.
                if (buffer.Length == previousLength)
                    throw new InvalidOperationException("Stream ended without CRLF");
                previousLength = buffer.Length;

                // The window is only widened once it is actually full; otherwise the next pass asks
                // for the same amount again and simply waits for more of it.
                if (buffer.Length >= count)
                {
                    if (count >= maxLength)
                        throw new InvalidOperationException("Stream not contain crlf");
                    count = Math.Min(count * 2, maxLength);
                }

                // keep last byte in next search window to handle \r\n split across chunks
                searchFrom = Math.Max(0, buffer.Length - 1);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int length = _ReadPreBuffer(buffer, offset, count);
            if (length > 0)
            {
                return length;
            }
            else
            {
                return _baseStream.Read(buffer, offset, count);
            }
        }
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int length = _ReadPreBuffer(buffer, offset, count);
            if (length > 0)
            {
                return length;
            }
            else
            {
                return await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            }
        }

#if NET6_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_preReadBuffer is not null && _preReadLength > 0)
            {
                int canCopy = Math.Min(buffer.Length, _preReadLength);
                _preReadBuffer.AsMemory(_preReadOffset, canCopy).CopyTo(buffer);
                _preReadOffset += canCopy;
                if (_preReadOffset >= _preReadBuffer.Length)
                {
                    _preReadBuffer = null;
                    _preReadOffset = 0;
                }
                return ValueTask.FromResult(canCopy);
            }
            return _baseStream.ReadAsync(buffer, cancellationToken);
        }
#endif

        int _ReadPreBuffer(byte[] buffer, int offset, int count)
        {
            if (_preReadBuffer is not null && _preReadLength > 0)
            {
                int canCopy = Math.Min(Math.Min(buffer.Length - offset, count), _preReadLength);
                Array.Copy(_preReadBuffer, _preReadOffset, buffer, offset, canCopy);
                _preReadOffset += canCopy;
                if (_preReadOffset >= _preReadBuffer.Length)
                {
                    _preReadBuffer = null;
                    _preReadOffset = 0;
                }
                return canCopy;
            }
            return 0;
        }

        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
