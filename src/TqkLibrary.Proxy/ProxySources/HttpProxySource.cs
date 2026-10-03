using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource : IProxySource, IHttpProxy
    {
        private readonly ILoggerFactory? _loggerFactory;
        readonly Uri _proxy;
        public ProxyCredential? Credential { get; set; }
        /// <summary>
        /// When above 0, sends the host of the
        /// <c>CONNECT host:port HTTP/1.1</c> line this many bytes per TCP segment, and the rest of the
        /// request around it in one write each, so a DPI box reading single
        /// segments never sees the whole target name. 0 (the default) sends it in one piece.
        /// </summary>
        public int ConnectRequestChunkSize
        {
            get => _connectRequestChunkSize;
            set => _connectRequestChunkSize = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        int _connectRequestChunkSize;

        /// <summary>
        /// When above 0, the ClientHello written into the tunnel is re-framed into several TLS records
        /// around its SNI host name, the name itself this many bytes per record, each record in its own
        /// segment; everything after the ClientHello is relayed as usual. See <see cref="StreamHelpers.TlsHandshakeChunkingStream"/>.
        /// 0 (the default) leaves the tunnel alone.
        /// </summary>
        public int TlsHandshakeChunkSize
        {
            get => _tlsHandshakeChunkSize;
            set => _tlsHandshakeChunkSize = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        int _tlsHandshakeChunkSize;
        /// <summary>
        /// Construct from a <c>http(s)://[user:pass@]host:port</c> URI. <paramref name="proxy"/> host may be a domain, IPv4, or IPv6 literal (in brackets).
        /// </summary>
        public HttpProxySource(Uri proxy, ILoggerFactory? loggerFactory = null)
        {
            if (proxy is null) throw new ArgumentNullException(nameof(proxy));
            if (!"http".Equals(proxy.Scheme, StringComparison.OrdinalIgnoreCase) &&
                !"https".Equals(proxy.Scheme, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Uri scheme must be 'http' or 'https', got '{proxy.Scheme}'", nameof(proxy));
            if (proxy.Port <= 0) throw new ArgumentException($"Uri must include a port: '{proxy}'", nameof(proxy));

            _proxy = proxy;
            _loggerFactory = loggerFactory;
            if (!string.IsNullOrWhiteSpace(_proxy.UserInfo))
            {
                var split = _proxy.UserInfo.Split(':');
                if (split.Length == 2)
                {
                    Credential = new ProxyCredential(split[0], split[1]);
                }
            }
        }
        // Neither IUdpCapable nor IBindCapable: the HTTP proxy protocol has no datagram and no
        // listen, so there is nothing to configure and nothing to ask for. There is no address
        // family setting either — CONNECT hands the upstream a name and the upstream resolves it,
        // so the IsSupportIpv6 that used to sit here had nothing to filter and no reader; a host
        // that set it believed it had turned something off.

        /// <summary>Nothing to release: this source holds no session, only the address of one.</summary>
        public ValueTask DisposeAsync() => default;

        public virtual Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnectSource>(new ConnectTunnel(this, tunnelId));
        }
    }
}
