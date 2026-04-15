using System.Diagnostics;
using System.Text;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshCli.Exceptions;

namespace TqkLibrary.Proxy.SshCli
{
    public class OpenSshConnectSource : IConnectSource
    {
        private readonly SshProcessRunner _runner;
        private readonly int _probeTimeoutMs;
        private Process? _process;
        private ProcessStdioStream? _stream;
        private readonly StringBuilder _stderrBuffer = new StringBuilder();
        private int _disposed;

        internal OpenSshConnectSource(SshProcessRunner runner, int probeTimeoutMs)
        {
            _runner = runner;
            _probeTimeoutMs = probeTimeoutMs;
        }

        public async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            CheckDisposed();
            if (_process != null) throw new InvalidOperationException("Already connected.");

            var process = _runner.StartForwardW(address.Host, address.Port);
            process.ErrorDataReceived += OnStdErr;
            process.BeginErrorReadLine();

            try
            {
                // Short grace: if ssh fails (auth, DNS, etc.) it usually exits within a few hundred ms.
                var graceMs = Math.Min(500, _probeTimeoutMs);
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linked.CancelAfter(graceMs);
                    try
                    {
#if NET6_0_OR_GREATER
                        await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
#else
                        await Task.Run(() => process.WaitForExit(graceMs), linked.Token).ConfigureAwait(false);
#endif
                    }
                    catch (OperationCanceledException) { }
                }

                if (process.HasExited)
                {
                    throw new InitConnectSourceFailedException(
                        $"ssh exited early (code={process.ExitCode}): {_stderrBuffer.ToString().Trim()}");
                }

                _process = process;
                _stream = new ProcessStdioStream(process);
            }
            catch
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
                try { process.Dispose(); } catch { }
                throw;
            }
        }

        public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (_stream is null)
                throw new InvalidOperationException($"Must call {nameof(ConnectAsync)} first.");
            return Task.FromResult<Stream>(_stream);
        }

        private void OnStdErr(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (_stderrBuffer)
            {
                if (_stderrBuffer.Length < 4096)
                    _stderrBuffer.AppendLine(e.Data);
            }
        }

        private void CheckDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(OpenSshConnectSource));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { _stream?.Dispose(); } catch { }
            if (_stream == null && _process != null)
            {
                try { if (!_process.HasExited) _process.Kill(); } catch { }
                try { _process.Dispose(); } catch { }
            }
        }
    }
}
