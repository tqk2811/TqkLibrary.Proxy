using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public class HttpProxySource : IProxySource, IHttpProxy
    {
        readonly Uri proxy;
        readonly ICredentials credentials;
        public HttpProxySource(Uri proxy)
        {
            this.proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource(Uri proxy, ICredentials credentials) : this(proxy)
        {
            this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        }

        public bool IsSupportUdp => false;

        public bool IsSupportTransferHttps => false;

        public bool IsSupportIpv6 => true;

        public async Task<ISessionSource> InitSessionAsync(Uri address, string host = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            return null;
        }
    }
}
