using System.Net;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource : IProxySource, ISocks4Proxy
    {
        readonly IPEndPoint iPEndPoint;
        readonly string userId;
        public Socks4ProxySource(IPEndPoint iPEndPoint, string? userId = null)
        {
            this.iPEndPoint = iPEndPoint ?? throw new ArgumentNullException(nameof(iPEndPoint));
            this.userId = userId ?? string.Empty;
        }

        public bool IsUseSocks4A { get; set; } = true;
        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => false;
        public bool IsSupportBind { get; set; } = true;

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
        }
    }
}
