using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshNet.Exceptions;

namespace TqkLibrary.Proxy.SshNet
{
    public class SshNetProxySource : IManagedProxySource, ISsh, IDisposable
    {
        // SSH.NET raises nothing useful when a session simply stops answering, so the state is read
        // rather than waited for. Fifteen seconds is a status refresh, not a failure detector — the
        // connection itself is what notices a dead peer, through KeepAliveInterval when one is set.
        private static readonly TimeSpan HealthPollInterval = TimeSpan.FromSeconds(15);

        private readonly SshNetConnectionOptions _options;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ILogger? _logger;
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);
        private SshClient? _client;
        private int _disposed;

        public SshNetProxySource(SshNetConnectionOptions options, ILoggerFactory? loggerFactory = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<SshNetProxySource>();
        }

        // Neither IUdpCapable nor IBindCapable: a direct-tcpip channel forwards one stream and
        // nothing else. No address family setting either — the destination is handed to the far end
        // as a name and resolved there.

        /// <summary>
        /// True while an authenticated session is open. Every tunnel is a channel on that one
        /// session, so this is what says whether any of them can be opened at all.
        /// </summary>
        public bool IsRunning => _client?.IsConnected == true;

        public string Endpoint => $"{_options.User}@{_options.Host}:{_options.Port}";

        /// <summary>
        /// Authenticates the session now instead of on the first tunnel, so the cost of the
        /// handshake does not land on a request.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Completes once the session is down, which for SSH.NET means finished: the client does not
        /// re-authenticate on its own, so there is no self-repair to sit out.
        /// </summary>
        /// <remarks>
        /// The state is what is watched, not one particular <see cref="SshClient"/>. A request
        /// arriving in the meantime reconnects through <c>EnsureConnectedAsync</c>, and a host that
        /// was told the way out had failed because of an object it never knew about would be
        /// rebuilding a source that had already mended itself.
        /// </remarks>
        public async Task<string> WaitUntilDownAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (!IsRunning) return $"the SSH session to {_options.Host} is closed";
                try { await Task.Delay(HealthPollInterval, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            return "cancelled";
        }

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            var client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            return new SshNetConnectSource(client, _options, _loggerFactory);
        }


        private async Task<SshClient> EnsureConnectedAsync(CancellationToken cancellationToken)
        {
            var existing = _client;
            if (existing != null && existing.IsConnected) return existing;

            await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_client != null && _client.IsConnected) return _client;

                if (_client != null)
                {
                    try { _client.Disconnect(); } catch { }
                    try { _client.Dispose(); } catch { }
                    _client = null;
                }

                var client = BuildClient();
                try
                {
#if NET6_0_OR_GREATER
                    await client.ConnectAsync(cancellationToken).ConfigureAwait(false);
#else
                    await Task.Run(() => client.Connect(), cancellationToken).ConfigureAwait(false);
#endif
                }
                catch (Exception ex)
                {
                    try { client.Dispose(); } catch { }
                    throw new SshNetException($"SSH connect failed: {ex.Message}", ex);
                }

                _client = client;
                _logger?.LogInformation("SSH connected to {Host}:{Port} as {User}", _options.Host, _options.Port, _options.User);
                return client;
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private SshClient BuildClient()
        {
            var methods = new List<AuthenticationMethod>();
            if (!string.IsNullOrEmpty(_options.Password))
            {
                methods.Add(new PasswordAuthenticationMethod(_options.User, _options.Password));
            }
            if (!string.IsNullOrEmpty(_options.IdentityFile) || _options.IdentityKeyBytes != null)
            {
                PrivateKeyFile keyFile;
                if (_options.IdentityKeyBytes != null)
                {
                    using var ms = new MemoryStream(_options.IdentityKeyBytes);
                    keyFile = string.IsNullOrEmpty(_options.IdentityFilePassphrase)
                        ? new PrivateKeyFile(ms)
                        : new PrivateKeyFile(ms, _options.IdentityFilePassphrase);
                }
                else
                {
                    keyFile = string.IsNullOrEmpty(_options.IdentityFilePassphrase)
                        ? new PrivateKeyFile(_options.IdentityFile!)
                        : new PrivateKeyFile(_options.IdentityFile!, _options.IdentityFilePassphrase);
                }
                methods.Add(new PrivateKeyAuthenticationMethod(_options.User, keyFile));
            }
            if (methods.Count == 0)
                throw new SshNetException("No authentication method configured. Set Password or IdentityFile/IdentityKeyBytes.");

            var info = new ConnectionInfo(_options.Host, _options.Port, _options.User, methods.ToArray())
            {
                Timeout = _options.ConnectionTimeout,
            };

            var client = new SshClient(info);
            if (_options.KeepAliveInterval > TimeSpan.Zero)
                client.KeepAliveInterval = _options.KeepAliveInterval;

            if (_options.HostKeyFingerprintsSha256.Count > 0)
            {
                var allowed = new HashSet<string>(_options.HostKeyFingerprintsSha256, StringComparer.OrdinalIgnoreCase);
                client.HostKeyReceived += (s, e) =>
                {
                    var fp = NormalizeFingerprint(e.FingerPrintSHA256);
                    e.CanTrust = allowed.Contains(fp);
                    if (!e.CanTrust)
                    {
                        _logger?.LogWarning("Rejecting host key {Fingerprint} (not in allow-list)", fp);
                    }
                };
            }

            return client;
        }

        private static string NormalizeFingerprint(string? sha256)
        {
            if (sha256 is null || sha256.Length == 0) return string.Empty;
            // SSH.NET returns base64 without "SHA256:" prefix; trim any padding for stable compare.
            var v = sha256.Trim();
            if (v.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
                v = v.Substring("SHA256:".Length);
            return v.TrimEnd('=');
        }

        private void CheckDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(SshNetProxySource));
        }

        /// <remarks>
        /// The work itself is still synchronous — SSH.NET only offers a blocking Disconnect — so this hands back a completed
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
            if (_client != null)
            {
                try { _client.Disconnect(); } catch { }
                try { _client.Dispose(); } catch { }
                _client = null;
            }
            _connectLock.Dispose();
        }
    }
}
