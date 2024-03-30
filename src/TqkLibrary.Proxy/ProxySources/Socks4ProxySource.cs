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

        public IConnectSource GetConnectSource()
        {
            return new ConnectTunnel(this);
        }

        public IBindSource GetBindSource()
        {
            return new BindTunnel(this);
        }

        public IUdpAssociateSource GetUdpAssociateSource()
        {
            throw new NotSupportedException();
        }
    }
}
