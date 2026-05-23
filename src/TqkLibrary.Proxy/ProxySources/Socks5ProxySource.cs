using System.Net;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource : IProxySource, ISocks5Proxy
    {
        private readonly ILoggerFactory? _loggerFactory;
        public Uri Uri { get; }
        public ProxyCredential? Credential { get; }
        public Socks5ProxySource(IPEndPoint iPEndPoint, ILoggerFactory? loggerFactory = null)
        {
            if (iPEndPoint is null) throw new ArgumentNullException(nameof(iPEndPoint));
            Uri = new UriBuilder("socks5", iPEndPoint.Address.ToString(), iPEndPoint.Port).Uri;
            _loggerFactory = loggerFactory;
        }
        public Socks5ProxySource(IPEndPoint iPEndPoint, ProxyCredential credential, ILoggerFactory? loggerFactory = null) : this(iPEndPoint, loggerFactory)
        {
            Credential = credential ?? throw new ArgumentNullException(nameof(credential));
        }

        /// <summary>
        /// Construct from a <c>socks5://[user:pass@]host[:port]</c> URI. <paramref name="uri"/> host may be a domain, IPv4, or IPv6 literal (in brackets).
        /// </summary>
        public Socks5ProxySource(Uri uri, ILoggerFactory? loggerFactory = null)
        {
            if (uri is null) throw new ArgumentNullException(nameof(uri));
            if (!"socks5".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Uri scheme must be 'socks5', got '{uri.Scheme}'", nameof(uri));
            if (uri.Port <= 0) throw new ArgumentException($"Uri must include a port: '{uri}'", nameof(uri));

            Uri = uri;

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                string userInfo = Uri.UnescapeDataString(uri.UserInfo);
                int colonIdx = userInfo.IndexOf(':');
                if (colonIdx > 0)
                {
                    string user = userInfo.Substring(0, colonIdx);
                    string pass = userInfo.Substring(colonIdx + 1);
                    Credential = new ProxyCredential(user, pass);
                }
            }

            _loggerFactory = loggerFactory;
        }

        public virtual bool IsSupportUdp { get; set; } = true;
        public virtual bool IsSupportIpv6 { get; set; } = true;
        public virtual bool IsSupportBind { get; set; } = true;

        public virtual Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnectSource>(new ConnectTunnel(this, tunnelId));
        }

        public virtual Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IBindSource>(new BindTunnel(this, tunnelId));
        }

        public virtual Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUdpAssociateSource>(new UdpTunnel(this, tunnelId));
        }
    }
}
