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

        public virtual IConnectSource GetConnectSource(Guid tunnelId)
        {
            return new ConnectTunnel(this, tunnelId);
        }

        public virtual IBindSource GetBindSource(Guid tunnelId)
        {
            return new BindTunnel(this, tunnelId);
        }

        public virtual IUdpAssociateSource GetUdpAssociateSource(Guid tunnelId)
        {
            throw new NotSupportedException();
        }
    }
}
