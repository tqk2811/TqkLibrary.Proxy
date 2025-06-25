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
            if (!string.IsNullOrWhiteSpace(_proxy.UserInfo))
            {
                var split = _proxy.UserInfo.Split(':');
                if (split.Length == 2)
                {
                    HttpProxyAuthentication = new HttpProxyAuthentication(split[0], split[1]);
                }
            }
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource(Uri proxy, HttpProxyAuthentication httpProxyAuthentication) : this(proxy)
        {
            HttpProxyAuthentication = httpProxyAuthentication ?? throw new ArgumentNullException(nameof(httpProxyAuthentication));
        }

        public virtual bool IsSupportUdp => false;
        public virtual bool IsSupportIpv6 { get; set; } = true;
        public virtual bool IsSupportBind => false;

        public virtual Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnectSource>(new ConnectTunnel(this, tunnelId));
        }

        public virtual Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public virtual Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
