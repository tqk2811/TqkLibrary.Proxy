using System.Net;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource : IProxySource, ISocks5Proxy
    {
        public IPEndPoint IPEndPoint { get; }
        public HttpProxyAuthentication? HttpProxyAuthentication { get; }
        public Socks5ProxySource(IPEndPoint iPEndPoint)
        {
            IPEndPoint = iPEndPoint ?? throw new ArgumentNullException(nameof(iPEndPoint));
        }
        public Socks5ProxySource(IPEndPoint iPEndPoint, HttpProxyAuthentication httpProxyAuthentication) : this(iPEndPoint)
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
            throw new NotSupportedException();
            //return new UdpTunnel(this);
        }
    }
}
