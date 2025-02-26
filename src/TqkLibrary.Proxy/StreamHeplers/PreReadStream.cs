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

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
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
            else
            {
                return await _baseStream.ReadAsync(buffer, offset, count);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }








        public override bool CanSeek => false;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();




        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        //{
        //    if (_preReadBuffer is not null)
        //    {
        //        int canReadSize = Math.Min(buffer.Length - offset, count);
        //        if (canReadSize >= _preReadBuffer.Length)
        //        {
        //            int lengthCopy = _preReadBuffer.Length;
        //            Array.Copy(_preReadBuffer, 0, buffer, offset, lengthCopy);
        //            _preReadBuffer = null;
        //            return lengthCopy;
        //        }
        //        else
        //        {
        //            int lengthCopy = canReadSize;
        //            Array.Copy(_preReadBuffer, 0, buffer, offset, lengthCopy);
        //            _preReadBuffer = _preReadBuffer.Skip(lengthCopy).ToArray();
        //            return lengthCopy;
        //        }
        //    }
        //    else
        //    {
        //        return _baseStream.Read(buffer, offset, count);
        //    }
        //}
        public override void Write(byte[] buffer, int offset, int count)
            => throw new NotSupportedException();
        //{
        //    _baseStream.Write(buffer, offset, count);
        //}

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => throw new NotSupportedException();

        public override void EndWrite(IAsyncResult asyncResult)
            => throw new NotSupportedException();


        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => throw new NotSupportedException();
        //{
        //    if (_preReadBuffer is not null)
        //    {
        //        return new AsyncResult(this, buffer, offset, count, callback, state).TryCallback();
        //    }
        //    else
        //    {
        //        return _baseStream.BeginRead(buffer, offset, count, callback, state);
        //    }
        //}
        public override int EndRead(IAsyncResult asyncResult)
            => throw new NotSupportedException();
        //{
        //    if (asyncResult is AsyncResult myAsyncResult)
        //    {
        //        return myAsyncResult.ReadByte(this);
        //    }
        //    else
        //    {
        //        return _baseStream.EndRead(asyncResult);
        //    }
        //}


        //class AsyncResult : IAsyncResult
        //{
        //    readonly PreReadStream _preReadStream;
        //    readonly byte[] _buffer;
        //    readonly int _offset;
        //    readonly int _count;
        //    readonly AsyncCallback? _callback;
        //    readonly object? _state;
        //    readonly ManualResetEvent manualResetEvent = new ManualResetEvent(false);
        //    public AsyncResult(PreReadStream preReadStream, byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        //    {
        //        _preReadStream = preReadStream;
        //        _buffer = buffer;
        //        _offset = offset;
        //        _count = count;
        //        _callback = callback;
        //        _state = state;
        //        manualResetEvent.Set();
        //    }

        //    public object? AsyncState => _state;
        //    public WaitHandle AsyncWaitHandle => manualResetEvent;
        //    public bool CompletedSynchronously => true;
        //    public bool IsCompleted => true;


        //    public IAsyncResult TryCallback()
        //    {
        //        if (_callback is not null)
        //            Task.Run(() => _callback.Invoke(this));
        //        return this;
        //    }

        //    public int ReadByte(PreReadStream preReadStream)
        //    {
        //        if (preReadStream._preReadBuffer is null || preReadStream._preReadBuffer.Length == 0)
        //        {
        //            preReadStream._preReadBuffer = null;
        //            return 0;
        //        }

        //        int canReadSize = Math.Min(_buffer.Length - _offset, _count);
        //        if (canReadSize >= preReadStream._preReadBuffer.Length)
        //        {
        //            int lengthCopy = preReadStream._preReadBuffer.Length;
        //            Array.Copy(preReadStream._preReadBuffer, 0, _buffer, _offset, lengthCopy);
        //            preReadStream._preReadBuffer = null;
        //            return lengthCopy;
        //        }
        //        else
        //        {
        //            int lengthCopy = canReadSize;
        //            Array.Copy(preReadStream._preReadBuffer, 0, _buffer, _offset, lengthCopy);
        //            preReadStream._preReadBuffer = preReadStream._preReadBuffer.Skip(lengthCopy).ToArray();
        //            return lengthCopy;
        //        }
        //    }
        //}
    }
}
