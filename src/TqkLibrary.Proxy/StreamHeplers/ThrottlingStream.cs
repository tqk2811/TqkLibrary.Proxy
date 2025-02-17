using System;
using System.Collections.Generic;
using System.Text;

namespace TqkLibrary.Proxy.StreamHeplers
{
    public class ThrottlingStream : Stream
    {
        readonly Stream _baseStream;
        readonly bool _disposeBaseStream;

        /// <summary>
        /// less or equal zero mean no limit
        /// </summary>
        public int ReadBytesPerTime { get; set; } = 0;
        /// <summary>
        /// less or equal zero mean no limit
        /// </summary>
        public int WriteBytesPerTime { get; set; } = 0;
        /// <summary>
        /// less or equal zero mean no limit
        /// </summary>
        public TimeSpan Time { get; set; } = TimeSpan.FromSeconds(1);


        public ThrottlingStream(Stream baseStream, bool disposeBaseStream = true)
        {
            this._baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            this._disposeBaseStream = disposeBaseStream;
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_disposeBaseStream)
                _baseStream.Dispose();
        }
        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }
        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);



        readonly object _lock_read = new();
        DateTime _lastTimeRead = DateTime.MinValue;
        int _readBytes = 0;
        int _CalcRead(int count)
        {
            if (ReadBytesPerTime == 0 || Time == TimeSpan.Zero) return count;
            lock (_lock_read)
            {
                DateTime now = DateTime.Now;
                if (now > _lastTimeRead.Add(Time))
                {
                    _lastTimeRead = now;
                    _readBytes = 0;
                }
                else
                {
                    count = Math.Max(0, ReadBytesPerTime - _readBytes);
                    _readBytes += count;
                }
                return count;
            }
        }
        public override int Read(byte[] buffer, int offset, int count)
            => _baseStream.Read(buffer, offset, _CalcRead(count));
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => _baseStream.BeginRead(buffer, offset, _CalcRead(count), callback, state);
        public override int EndRead(IAsyncResult asyncResult)
            => _baseStream.EndRead(asyncResult);


        readonly object _lock_write = new();
        DateTime _lastTimeWrite = DateTime.MinValue;
        int _writeBytes = 0;
        int _CalcWrite(int count)
        {
            if (WriteBytesPerTime == 0 || Time == TimeSpan.Zero) return count;
            lock (_lock_write)
            {
                DateTime now = DateTime.Now;
                if (now > _lastTimeWrite.Add(Time))
                {
                    _lastTimeWrite = now;
                    _writeBytes = 0;
                }
                else
                {
                    count = Math.Max(0, WriteBytesPerTime - _writeBytes);
                    _writeBytes += count;
                }
                return count;
            }
        }
        public override void Write(byte[] buffer, int offset, int count)
            => _baseStream.Write(buffer, offset, _CalcWrite(count));
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => _baseStream.BeginWrite(buffer, offset, _CalcWrite(count), callback, state);
        public override void EndWrite(IAsyncResult asyncResult)
            => _baseStream.EndWrite(asyncResult);
    }
}
