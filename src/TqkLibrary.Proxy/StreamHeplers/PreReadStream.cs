using System.Text;
using TqkLibrary.Streams;

namespace TqkLibrary.Proxy.StreamHeplers
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
        /// 
        /// </summary>
        /// <param name="maxLength">Http header size or url max length</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<string> PreReadLineAsync(int maxLength = 32 * 1024, CancellationToken cancellationToken = default)
        {
            byte[] buffer;
            do
            {
                buffer = await PreReadAsync(maxLength, cancellationToken).ConfigureAwait(false);
                string text = Encoding.ASCII.GetString(buffer);
                int index_cr = Array.IndexOf(buffer, (byte)'\r');
                int index_lf = Array.IndexOf(buffer, (byte)'\n');
                if (index_cr + 1 == index_lf)
                    return Encoding.ASCII.GetString(buffer, 0, index_lf + 1);

                if (buffer.Length == maxLength)
                    throw new InvalidOperationException("Stream not contain crlf");
            }
            while (true);
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
