using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource : IProxySource, IHttpProxy
    {
        readonly Uri _proxy;
        public HttpProxyAuthentication? HttpProxyAuthentication { get; set; }
        public HttpProxySource(Uri proxy)
        {
            this._proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource(Uri proxy, HttpProxyAuthentication httpProxyAuthentication) : this(proxy)
        {
            this.HttpProxyAuthentication = httpProxyAuthentication ?? throw new ArgumentNullException(nameof(httpProxyAuthentication));
        }

        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => false;

        public IConnectSource GetConnectSource()
        {
            return new ConnectTunnel(this);
        }

        public IBindSource GetBindSource()
        {
            throw new NotSupportedException();
        }

        public IUdpAssociateSource GetUdpAssociateSource()
        {
            throw new NotSupportedException();
        }
    }
}
