using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource : IProxySource, IHttpProxy
    {
        readonly Uri _proxy;
        public HttpProxyAuthentication? HttpProxyAuthentication { get; set; }
        public HttpProxySource(Uri proxy)
        {
            _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource(Uri proxy, HttpProxyAuthentication httpProxyAuthentication) : this(proxy)
        {
            HttpProxyAuthentication = httpProxyAuthentication ?? throw new ArgumentNullException(nameof(httpProxyAuthentication));
        }

        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 { get; set; } = true;
        public bool IsSupportBind => false;

        public IConnectSource GetConnectSource(Guid tunnelId)
        {
            return new ConnectTunnel(this, tunnelId);
        }

        public IBindSource GetBindSource(Guid tunnelId)
        {
            throw new NotSupportedException();
        }

        public IUdpAssociateSource GetUdpAssociateSource(Guid tunnelId)
        {
            throw new NotSupportedException();
        }
    }
}
