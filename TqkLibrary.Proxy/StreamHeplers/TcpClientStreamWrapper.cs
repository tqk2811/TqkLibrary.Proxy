using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal class TcpClientStreamWrapper : Stream
    {
        readonly TcpClient _tcpClient;
        readonly Stream _stream;
        public TcpClientStreamWrapper(TcpClient tcpClient)
        {
            this._tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            this._stream = tcpClient.GetStream();
        }
        protected override void Dispose(bool disposing)
        {
            _stream.Dispose();
            _tcpClient.Dispose();
            base.Dispose(disposing);
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position { get => _stream.Position; set => _stream.Position = value; }
        public override bool CanTimeout => _stream.CanTimeout;
        public override int ReadTimeout { get => _stream.ReadTimeout; set => _stream.ReadTimeout = value; }
        public override int WriteTimeout { get => _stream.WriteTimeout; set => _stream.WriteTimeout = value; }


        public override void Flush()
            => _stream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => _stream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => _stream.Seek(offset, origin);

        public override void SetLength(long value)
            => _stream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
            => _stream.Write(buffer, offset, count);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => _stream.BeginRead(buffer, offset, count, callback, state);
        public override int EndRead(IAsyncResult asyncResult)
            => _stream.EndRead(asyncResult);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            => _stream.BeginWrite(buffer, offset, count, callback, state);
        public override void EndWrite(IAsyncResult asyncResult)
            => _stream.EndWrite(asyncResult);

        public override void Close()
            => _stream.Close();

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
            => _stream.CopyToAsync(destination, bufferSize, cancellationToken);

        public override int GetHashCode()
            => _stream.GetHashCode();
    }
}
