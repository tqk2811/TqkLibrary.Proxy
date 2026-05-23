using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshNet.Exceptions;

namespace TqkLibrary.Proxy.SshNet
{
    public class SshNetProxySource : IProxySource, ISsh, IDisposable
    {
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

        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => false;

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            var client = await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);
            return new SshNetConnectSource(client, _options, _loggerFactory);
        }

        public Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("SSH does not support BIND.");

        public Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("SSH does not support UDP.");

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
