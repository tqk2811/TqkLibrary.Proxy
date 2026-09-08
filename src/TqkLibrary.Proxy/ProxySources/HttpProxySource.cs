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
        public virtual bool IsSupportUdp => false;
        public virtual bool IsSupportIpv6 { get; set; } = true;
        public virtual bool IsSupportBind => false;

        /// <summary>Nothing to release: this source holds no session, only the address of one.</summary>
        public ValueTask DisposeAsync() => default;

        public virtual Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnectSource>(new ConnectTunnel(this, tunnelId));
        }

        public virtual Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public virtual Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
