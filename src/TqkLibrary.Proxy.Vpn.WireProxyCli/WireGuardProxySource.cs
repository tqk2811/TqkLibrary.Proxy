using System.Net;
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
        private readonly ILoggerFactory? _loggerFactory;
        private readonly object _socks5Lock = new object();
        private Socks5ProxySource _socks5;
        private IPEndPoint _socks5Endpoint;
        private int _disposed;

        public WireGuardProxySource(WireGuardOptions options, ILoggerFactory? loggerFactory = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _runner = new WireProxyProcessRunner(options);
            _loggerFactory = loggerFactory;

            _socks5Endpoint = _runner.Socks5Endpoint;
            _socks5 = BuildSocks5(_socks5Endpoint);
        }

        private Socks5ProxySource BuildSocks5(IPEndPoint endpoint)
        {
            ProxyCredential? auth = null;
            if (!string.IsNullOrEmpty(_options.Socks5Username) && !string.IsNullOrEmpty(_options.Socks5Password))
                auth = new ProxyCredential(_options.Socks5Username!, _options.Socks5Password!);

            var socks5 = auth != null
                ? new Socks5ProxySource(endpoint, auth, _loggerFactory)
                : new Socks5ProxySource(endpoint, _loggerFactory);

            socks5.IsSupportUdp = _options.IsSupportUdp;
            socks5.IsSupportIpv6 = _options.IsSupportIpv6;
            socks5.IsSupportBind = false;
            return socks5;
        }

        /// <summary>
        /// The SOCKS5 client pointed at wherever the tunnel currently listens. A restart picks a
        /// new port, and <see cref="Socks5ProxySource"/> fixes its address at construction, so the
        /// client is rebuilt when the runner moves rather than dialling the old port.
        /// </summary>
        private Socks5ProxySource CurrentSocks5()
        {
            IPEndPoint endpoint = _runner.Socks5Endpoint;
            lock (_socks5Lock)
            {
                if (!endpoint.Equals(_socks5Endpoint))
                {
                    _socks5 = BuildSocks5(endpoint);
                    _socks5Endpoint = endpoint;
                }
                return _socks5;
            }
        }

        public bool IsSupportUdp => _options.IsSupportUdp;
        public bool IsSupportIpv6 => _options.IsSupportIpv6;
        public bool IsSupportBind => false;

        /// <summary>
        /// True while the wireproxy subprocess is running. It says the tunnel is available, not
        /// that the far end is answering — nothing short of sending traffic can tell you that.
        /// </summary>
        public bool IsRunning => _runner.IsAlive;

        /// <summary>
        /// The local SOCKS5 listener the tunnel is exposed on. Loopback, and — for a generated
        /// config — on a random port behind a random credential.
        /// </summary>
        public IPEndPoint Socks5Endpoint => _runner.Socks5Endpoint;

        /// <summary>
        /// Raised when the subprocess dies of its own accord, so a host that wants the tunnel
        /// permanently up can rebuild it immediately rather than at the next connection.
        /// Disposing this source does not raise it.
        /// </summary>
        public event EventHandler<WireProxyExitedEventArgs>? Exited
        {
            add => _runner.Exited += value;
            remove => _runner.Exited -= value;
        }

        /// <summary>
        /// Brings the tunnel up without asking for a connection, and returns once its SOCKS5
        /// listener is accepting.
        /// </summary>
        /// <remarks>
        /// Starting is otherwise lazy, which means the first connection through the VPN pays for
        /// the subprocess launch and the WireGuard handshake — seconds, on the request that
        /// happened to be first. A host that knows it will use this tunnel calls this at startup
        /// instead, and every request afterwards finds it already up.
        /// </remarks>
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            return _runner.EnsureStartedAsync(cancellationToken);
        }

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            await _runner.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            return await CurrentSocks5().GetConnectSourceAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        }

        public Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("wireproxy SOCKS5 does not support BIND.");

        public async Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (!IsSupportUdp)
                throw new NotSupportedException("UDP support is disabled. Set WireGuardOptions.IsSupportUdp = true if your wireproxy build supports SOCKS5 UDP ASSOCIATE.");
            await _runner.EnsureStartedAsync(cancellationToken).ConfigureAwait(false);
            return await CurrentSocks5().GetUdpAssociateSourceAsync(tunnelId, cancellationToken).ConfigureAwait(false);
        }

        private void CheckDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(WireGuardProxySource));
        }

        /// <remarks>
        /// The work itself is still synchronous — stopping wireproxy means killing a process and waiting for it to go — so this hands back a completed
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
            _runner.Dispose();
        }
    }
}
