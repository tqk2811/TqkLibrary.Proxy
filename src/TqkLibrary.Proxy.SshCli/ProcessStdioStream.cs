using System.Diagnostics;

namespace TqkLibrary.Proxy.SshCli
{
    internal sealed class ProcessStdioStream : Stream
    {
        private readonly Process _process;
        private readonly Stream _stdin;
        private readonly Stream _stdout;
        private int _disposed;

        public ProcessStdioStream(Process process)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _stdin = process.StandardInput.BaseStream;
            _stdout = process.StandardOutput.BaseStream;
        }

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _stdin.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _stdin.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => _stdout.Read(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _stdout.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Write(byte[] buffer, int offset, int count) => _stdin.Write(buffer, offset, count);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => _stdin.WriteAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (disposing)
            {
                try { _stdin.Dispose(); } catch { }
                try { _stdout.Dispose(); } catch { }
                try
                {
                    if (!_process.HasExited) _process.Kill();
                }
                catch { }
                try { _process.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
