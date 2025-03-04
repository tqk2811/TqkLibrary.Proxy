using System.Text;
using TqkLibrary.Streams;

namespace TqkLibrary.Proxy.StreamHeplers
{
    public class PreReadStream : BaseInheritStream
    {
        byte[]? _preReadBuffer;
        public PreReadStream(Stream baseStream, bool disposeBaseStream = true) : base(baseStream, disposeBaseStream)
        {
        }

        public async Task<byte[]> PreReadAsync(int count, CancellationToken cancellationToken = default)
        {
            if (_preReadBuffer is null || _preReadBuffer.Length < count)
            {
                int needRead = count - (_preReadBuffer?.Length ?? 0);
                byte[] buffer = new byte[needRead];
                int byte_read = await _baseStream.ReadAsync(buffer, 0, needRead, cancellationToken);

                if (_preReadBuffer is null) _preReadBuffer = buffer.Take(byte_read).ToArray();
                else _preReadBuffer = _preReadBuffer.Concat(buffer.Take(byte_read)).ToArray();

                return _preReadBuffer.ToArray();
            }
            else
            {
                return _preReadBuffer.Take(count).ToArray();
            }
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
        int _ReadPreBuffer(byte[] buffer, int offset, int count)
        {
            if (_preReadBuffer is not null)
            {
                int canReadSize = Math.Min(buffer.Length - offset, count);
                if (canReadSize >= _preReadBuffer.Length)
                {
                    int lengthCopy = _preReadBuffer.Length;
                    Array.Copy(_preReadBuffer, 0, buffer, offset, lengthCopy);
                    _preReadBuffer = null;
                    return lengthCopy;
                }
                else
                {
                    int lengthCopy = canReadSize;
                    Array.Copy(_preReadBuffer, 0, buffer, offset, lengthCopy);
                    _preReadBuffer = _preReadBuffer.Skip(lengthCopy).ToArray();
                    return lengthCopy;
                }
            }
            return -1;
        }

        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
