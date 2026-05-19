using System.Net;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource : IProxySource, ISocks5Proxy
    {
        private readonly ILoggerFactory? _loggerFactory;
        public IPEndPoint IPEndPoint { get; }
        public HttpProxyAuthentication? HttpProxyAuthentication { get; }
        public Socks5ProxySource(IPEndPoint iPEndPoint, ILoggerFactory? loggerFactory = null)
        {
            IPEndPoint = iPEndPoint ?? throw new ArgumentNullException(nameof(iPEndPoint));
            _loggerFactory = loggerFactory;
        }
        public Socks5ProxySource(IPEndPoint iPEndPoint, HttpProxyAuthentication httpProxyAuthentication, ILoggerFactory? loggerFactory = null) : this(iPEndPoint, loggerFactory)
        {
            HttpProxyAuthentication = httpProxyAuthentication ?? throw new ArgumentNullException(nameof(httpProxyAuthentication));
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
