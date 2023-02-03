using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource : IProxySource, IHttpProxy
    {
        readonly Uri proxy;
        readonly NetworkCredential networkCredential;
        public HttpProxySource(Uri proxy)
        {
            this.proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource(Uri proxy, NetworkCredential networkCredential) : this(proxy)
        {
            this.networkCredential = networkCredential ?? throw new ArgumentNullException(nameof(networkCredential));
        }

        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => false;

        public Task<IConnectSource> InitConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new Socks4ProxySourceTunnel(this, cancellationToken).InitConnectAsync(address);
        }

        public Task<IBindSource> InitBindAsync(Uri address, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
