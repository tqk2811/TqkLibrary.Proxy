using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TqkLibrary.Proxy.Handlers
{
    public class BaseProxyServerHandler : IProxyServerHandler
    {
        protected readonly IProxySource _proxySource;
        public BaseProxyServerHandler()
        {
            _proxySource = new LocalProxySource();
        }
        public BaseProxyServerHandler(IProxySource proxySource)
        {
            _proxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
        }
        public virtual Task<bool> IsAcceptUserAsync(IUserInfo userInfo, CancellationToken cancellationToken = default)
        {
            if (userInfo.Authentication is Socks5Authentication socks5Authentication)
            {
                foreach (var socks5_Auth in socks5Authentication.Auths)
                {
                    switch (socks5_Auth)
                    {
                        case Socks5_Auth.NoAuthentication:
                            socks5Authentication.Choice = socks5_Auth;
                            return Task.FromResult(true);

                        default:
                            continue;
                    }
                }
            }
            return Task.FromResult(true);
        }
        public virtual Task<bool> IsAcceptDomainAsync(Uri uri, IUserInfo userInfo, CancellationToken cancellationToken = default)
        {
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

            bool isAccept = true;
            switch (uri.HostNameType)
            {
                case UriHostNameType.Dns:
                    if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
                    {
                        isAccept = false;
                    }
                    break;

                case UriHostNameType.IPv4:
                    if (uri.Host.StartsWith("127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        isAccept = false;
                    }
                    break;

                case UriHostNameType.IPv6:
                    if (uri.Host.Equals("[::1]", StringComparison.OrdinalIgnoreCase))
                    {
                        isAccept = false;
                    }
                    break;
            }
            return Task.FromResult(isAccept);
        }
        public virtual Task<IProxySource> GetProxySourceAsync(Uri? uri, IUserInfo userInfo, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_proxySource);
        }
        public virtual Task<Stream> StreamHandlerAsync(Stream stream, IUserInfo userInfo, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(stream);
        }
    }
}
