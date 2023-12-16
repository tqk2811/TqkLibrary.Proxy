using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource : IProxySource, ISocks5Proxy
    {
        public IPEndPoint IPEndPoint { get; }
        public HttpProxyAuthentication? HttpProxyAuthentication { get; }
        public Socks5ProxySource(IPEndPoint iPEndPoint)
        {
            this.IPEndPoint = iPEndPoint ?? throw new ArgumentNullException(nameof(iPEndPoint));
        }
        public Socks5ProxySource(IPEndPoint iPEndPoint, HttpProxyAuthentication httpProxyAuthentication) : this(iPEndPoint)
        {
            this.HttpProxyAuthentication = httpProxyAuthentication ?? throw new ArgumentNullException(nameof(httpProxyAuthentication));
        }

        public bool IsSupportUdp => true;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => true;

        public IConnectSource GetConnectSource()
        {
            return new ConnectTunnel(this);
        }

        public IBindSource GetBindSource()
        {
            throw new NotSupportedException();
            //return new BindTunnel(this);
        }

        public IUdpAssociateSource GetUdpAssociateSource()
        {
            throw new NotSupportedException();
            //return new UdpTunnel(this);
        }
    }
}
