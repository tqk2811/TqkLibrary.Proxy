using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;

namespace TestProxy
{
    class CustomHttpProxyServerHandler : BaseProxyServerHandler
    {
        readonly ProxyCredential? _credential;
        public CustomHttpProxyServerHandler(IProxySource proxySource, ProxyCredential? credential = null) : base(proxySource)
        {
            _credential = credential;
        }

        public override async Task<bool> IsAcceptUserAsync(IUserInfo userInfo, CancellationToken cancellationToken = default)
        {
            if (userInfo.Authentication is ProxyCredential credential)
            {
                return credential.Equals(_credential);
            }
            return false;
        }
    }
}
