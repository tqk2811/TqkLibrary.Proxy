using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal class StreamHeaderWriter : Stream
    {
        public Stream BaseStream { get; }
        internal StreamHeaderWriter(Stream baseStream)
        {
            BaseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => BaseStream.CanWrite;

        public override long Length => BaseStream.Length;

        public override long Position { get => BaseStream.Position; set => BaseStream.Position = value; }

        public override void Flush()
        {
            BaseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
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
            BaseStream.Write(buffer, offset, count);
        }


        public Task WriteAsync(string text)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            return BaseStream.WriteAsync(buffer, 0, buffer.Length);
        }
        public Task WriteLineAsync(string text)
        {
            byte[] buffer = Encoding.ASCII.GetBytes(text + "\r\n");
            return BaseStream.WriteAsync(buffer, 0, buffer.Length);
        }

        static readonly byte[] line_break = new byte[] { 13, 10 };
        public Task WriteLineAsync()
        {
            return BaseStream.WriteAsync(line_break, 0, line_break.Length);
        }
    }
}
