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
