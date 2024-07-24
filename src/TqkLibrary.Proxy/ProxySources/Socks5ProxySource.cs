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
            //return new UdpTunnel(this);
        }
    }
}
