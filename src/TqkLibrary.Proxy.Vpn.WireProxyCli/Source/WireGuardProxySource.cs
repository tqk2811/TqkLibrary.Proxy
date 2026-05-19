using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    /// <summary>
    /// Application-layer WireGuard VPN exposed as an <see cref="IProxySource"/>.
    /// Internally spawns wireproxy (https://github.com/pufferffish/wireproxy) as a subprocess and
    /// forwards <see cref="GetConnectSourceAsync"/> calls through its local SOCKS5 listener.
    /// No OS-level TUN device is created — the WireGuard tunnel lives entirely in wireproxy's user space.
    /// </summary>
    public class WireGuardProxySource : IProxySource, ISocks5Proxy, IDisposable
    {
        private readonly WireGuardOptions _options;
        private readonly WireProxyProcessRunner _runner;
        private readonly Socks5ProxySource _socks5;
        private readonly ILoggerFactory? _loggerFactory;
        private int _disposed;

        public WireGuardProxySource(WireGuardOptions options, ILoggerFactory? loggerFactory = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _runner = new WireProxyProcessRunner(options);
            _loggerFactory = loggerFactory;

            HttpProxyAuthentication? auth = null;
            if (!string.IsNullOrEmpty(options.Socks5Username) && !string.IsNullOrEmpty(options.Socks5Password))
                auth = new HttpProxyAuthentication(options.Socks5Username!, options.Socks5Password!);

            _socks5 = auth != null
                ? new Socks5ProxySource(_runner.Socks5Endpoint, auth, loggerFactory)
                : new Socks5ProxySource(_runner.Socks5Endpoint, loggerFactory);

            _socks5.IsSupportUdp = options.IsSupportUdp;
            _socks5.IsSupportIpv6 = options.IsSupportIpv6;
            _socks5.IsSupportBind = false;
        }

        public bool IsSupportUdp => _socks5.IsSupportUdp;
        public bool IsSupportIpv6 => _socks5.IsSupportIpv6;
        public bool IsSupportBind => false;

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            await _runner.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            return await _socks5.GetConnectSourceAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        }

        public Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("wireproxy SOCKS5 does not support BIND.");

        public async Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (!IsSupportUdp)
                throw new NotSupportedException("UDP support is disabled. Set WireGuardOptions.IsSupportUdp = true if your wireproxy build supports SOCKS5 UDP ASSOCIATE.");
            await _runner.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            return await _socks5.GetUdpAssociateSourceAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        }

        private void CheckDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(WireGuardProxySource));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _runner.Dispose();
        }
    }
}
