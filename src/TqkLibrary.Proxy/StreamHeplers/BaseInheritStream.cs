namespace TqkLibrary.Proxy.StreamHeplers
{
    //https://devblogs.microsoft.com/pfxteam/overriding-stream-asynchrony/
    public class BaseInheritStream : Stream
    {
        protected readonly Stream _baseStream;
        readonly bool _disposeBaseStream;

        public BaseInheritStream(Stream baseStream, bool disposeBaseStream = true)
        {
            this._baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            this._disposeBaseStream = disposeBaseStream;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_disposeBaseStream) _baseStream.Dispose();
        }
#if NET5_0_OR_GREATER
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (_disposeBaseStream) await _baseStream.DisposeAsync();
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
        public override void Flush() => _baseStream.Flush();//call from FlushAsync
        public override Task FlushAsync(CancellationToken cancellationToken) => _baseStream.FlushAsync(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);
        public override void Close() => _baseStream.Close();



        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => _baseStream.BeginRead(buffer, offset, count, callback, state);//->_baseStream.Read & block threadpool (maybe)
        public override int EndRead(IAsyncResult asyncResult)
            => _baseStream.EndRead(asyncResult);
        public override int Read(byte[] buffer, int offset, int count)
            => _baseStream.Read(buffer, offset, count);




        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => _baseStream.BeginWrite(buffer, offset, count, callback, state);//->_baseStream.Write & block threadpool (maybe)
        public override void EndWrite(IAsyncResult asyncResult)
            => _baseStream.EndWrite(asyncResult);
        public override void Write(byte[] buffer, int offset, int count)
            => _baseStream.Write(buffer, offset, count);
    }
}
