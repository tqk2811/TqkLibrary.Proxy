using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal class StreamHeaderReader : Stream
    {
        public Stream BaseStream { get; }
        internal StreamHeaderReader(Stream baseStream)
        {
            BaseStream = baseStream;
        }
        public override bool CanRead => BaseStream.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return BaseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public async Task<string> ReadLineAsync()
        {
            using MemoryStream memoryStream = new MemoryStream();
            byte[] buffer = new byte[1];
            int totalRead = 0;
            while (true)
            {
                int byte_read = await BaseStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (byte_read == 0) throw new InvalidOperationException();
                totalRead += byte_read;

                if (totalRead > 40 * 1024) throw new InvalidOperationException();

                if (buffer[0] == 13)
                {
                    byte_read = await BaseStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                    if (byte_read == 0) throw new InvalidOperationException();
                    if (buffer[0] == 10) return Encoding.ASCII.GetString(memoryStream.ToArray());
                    else throw new InvalidOperationException();
                }
                else memoryStream.WriteByte(buffer[0]);
            }
        }
    }
}
