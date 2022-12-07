using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public class HttpProxySource : IProxySource, IHttpProxy
    {
        readonly Uri proxy;
        public HttpProxySource(Uri proxy)
        {
            this.proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource()
        {

        }

        public bool IsSupportUdp => false;

        public bool IsSupportTransferHttps => false;

        public bool IsSupportIpv6 => true;

        public Task<ISessionSource> InitSessionAsync(IPAddress iPAddress, ProtocolType protocolType)
        {
            throw new NotSupportedException();
        }

        public async Task<ISessionSource> InitSessionAsync(Uri address, bool isTransferHttps = false)
        {
            if (isTransferHttps) throw new NotSupportedException($"HttpProxy not support transfer https");
            if (address == null) throw new ArgumentNullException(nameof(address));
            return null;
        }
    }
}
