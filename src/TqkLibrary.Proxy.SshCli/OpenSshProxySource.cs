using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshCli.Exceptions;

namespace TqkLibrary.Proxy.SshCli
{
    public class OpenSshProxySource : IProxySource, ISsh, IDisposable
    {
        private readonly OpenSshConnectionOptions _options;
        private readonly SshProcessRunner _runner;
        private readonly SemaphoreSlim _masterLock = new SemaphoreSlim(1, 1);
        private readonly ILoggerFactory? _loggerFactory;
        private Process? _masterProcess;
        private int _disposed;

        public OpenSshProxySource(OpenSshConnectionOptions options, ILoggerFactory? loggerFactory = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _runner = new SshProcessRunner(options);
            _loggerFactory = loggerFactory;
        }

        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => false;

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (_runner.ControlMasterEnabled)
                await EnsureMasterAsync(cancellationToken).ConfigureAwait(false);

            return new OpenSshConnectSource(_runner, _options.ConnectProbeTimeoutMs);
        }

        public Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("ssh -W does not support BIND.");

        public Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("ssh -W does not support UDP.");

        private async Task EnsureMasterAsync(CancellationToken cancellationToken)
        {
            if (_masterProcess != null && !_masterProcess.HasExited) return;

            await _masterLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_masterProcess != null && !_masterProcess.HasExited) return;

                if (_masterProcess != null)
                {
                    try { _masterProcess.Dispose(); } catch { }
                    _masterProcess = null;
                }

                var process = _runner.StartMaster();
                // Wait until the control socket appears (master is ready) or the process dies.
                var deadline = DateTime.UtcNow.AddMilliseconds(_options.ConnectProbeTimeoutMs);
                while (DateTime.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (process.HasExited)
                    {
                        string err;
                        try { err = process.StandardError.ReadToEnd(); } catch { err = string.Empty; }
                        throw new SshProcessException(
                            $"ssh ControlMaster exited early (code={process.ExitCode}): {err.Trim()}",
                            process.ExitCode, err);
                    }
                    if (_runner.ControlSocketPath != null && File.Exists(_runner.ControlSocketPath))
                        break;
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }

                if (_runner.ControlSocketPath != null && !File.Exists(_runner.ControlSocketPath))
                {
                    try { if (!process.HasExited) process.Kill(); } catch { }
                    try { process.Dispose(); } catch { }
                    throw new SshProcessException("Timed out waiting for ssh ControlMaster socket.");
                }

                _masterProcess = process;
            }
            finally
            {
                _masterLock.Release();
            }
        }

        private void CheckDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(OpenSshProxySource));
        }

        /// <remarks>
        /// The work itself is still synchronous — closing the master connection means killing the ssh process — so this hands back a completed
        /// task rather than pretending otherwise. It exists because the owner releases a way out
        /// through <see cref="IProxySource"/> and should not have to know which shape of disposal a
        /// particular source happens to offer.
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                _runner.StopMaster();
            }
            catch { }
            if (_masterProcess != null)
            {
                try { if (!_masterProcess.HasExited) _masterProcess.Kill(); } catch { }
                try { _masterProcess.Dispose(); } catch { }
                _masterProcess = null;
            }
            _runner.Dispose();
            _masterLock.Dispose();
        }
    }
}
