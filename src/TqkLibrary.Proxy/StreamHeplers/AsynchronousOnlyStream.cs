namespace TqkLibrary.Proxy.StreamHeplers
{
    public class AsynchronousOnlyStream : Stream
    {
        readonly Stream _baseStream;
        public AsynchronousOnlyStream(Stream baseStream)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        }
        protected override void Dispose(bool disposing)
        {
            _baseStream.Dispose();
            base.Dispose(disposing);
        }
#if NET5_0_OR_GREATER
        public override ValueTask DisposeAsync()
        {
            return _baseStream.DisposeAsync();
        }
#endif

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }
        public override bool CanTimeout => _baseStream.CanTimeout;
        public override int ReadTimeout { get => _baseStream.ReadTimeout; set => _baseStream.ReadTimeout = value; }
        public override int WriteTimeout { get => _baseStream.WriteTimeout; set => _baseStream.WriteTimeout = value; }
        public override void Flush()
        {
            _baseStream.Flush();//call from FlushAsync
        }
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _baseStream.FlushAsync(cancellationToken);
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            return _baseStream.Seek(offset, origin);
        }
        public override void SetLength(long value)
        {
            _baseStream.SetLength(value);
        }
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return _baseStream.BeginRead(buffer, offset, count, callback, state);
        }
        public override int EndRead(IAsyncResult asyncResult)
        {
            return _baseStream.EndRead(asyncResult);
        }
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return _baseStream.BeginWrite(buffer, offset, count, callback, state);
        }
        public override void EndWrite(IAsyncResult asyncResult)
        {
            _baseStream.EndWrite(asyncResult);
        }
        public override void Close()
        {
            _baseStream.Close();
        }



        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException($"Use asynchronous method only");
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException($"Use asynchronous method only");
        }
#if NET5_0_OR_GREATER
        public override void CopyTo(Stream destination, int bufferSize)
        {
            throw new InvalidOperationException($"Use asynchronous method only");
        }
#endif
    }
}
