using System.Net;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource : IProxySource, IBindCapable, ISocks4Proxy
    {
        private readonly ILoggerFactory? _loggerFactory;
        public Uri Uri { get; }
        internal readonly string userId;
        public Socks4ProxySource(IPEndPoint iPEndPoint, string? userId = null, ILoggerFactory? loggerFactory = null)
        {
            if (iPEndPoint is null) throw new ArgumentNullException(nameof(iPEndPoint));
            this.Uri = new UriBuilder("socks4", iPEndPoint.Address.ToString(), iPEndPoint.Port).Uri;
            this.userId = userId ?? string.Empty;
            _loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Construct from a <c>socks4://[user@]host:port</c> URI. <paramref name="uri"/> host may be a domain or IPv4 literal (SOCKS4 itself does not support IPv6).
        /// </summary>
        public Socks4ProxySource(Uri uri, ILoggerFactory? loggerFactory = null)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));
            if (!"socks4".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Uri scheme must be 'socks4', got '{uri.Scheme}'", nameof(uri));
            if (uri.Port <= 0) throw new ArgumentException($"Uri must include a port: '{uri}'", nameof(uri));

            this.Uri = uri;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                string userInfo = global::System.Uri.UnescapeDataString(uri.UserInfo);
                int colonIdx = userInfo.IndexOf(':');
                this.userId = colonIdx >= 0 ? userInfo.Substring(0, colonIdx) : userInfo;
            }
            else
            {
                this.userId = string.Empty;
            }

            _loggerFactory = loggerFactory;
        }

        public bool IsUseSocks4A { get; set; } = true;

        // Not IUdpCapable: SOCKS4 has no UDP ASSOCIATE. Nor is there an address family setting —
        // the protocol has no address type for IPv6 at all, so this way out cannot carry it however
        // anything is configured, and a setting saying otherwise would be a lie rather than a knob.
        public bool IsSupportBind { get; set; } = true;

        /// <summary>Nothing to release: this source holds no session, only the address of one.</summary>
        public ValueTask DisposeAsync() => default;

        public virtual Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnectSource>(new ConnectTunnel(this, tunnelId));
        }

        public virtual Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IBindSource>(new BindTunnel(this, tunnelId));
        }
    }
}
