using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;

namespace TestProxy
{
    class CustomHttpProxyServerHandler : BaseProxyServerHandler
    {
        readonly HttpProxyAuthentication? _httpProxyAuthentication;
        public CustomHttpProxyServerHandler(IProxySource proxySource, HttpProxyAuthentication? httpProxyAuthentication = null) : base(proxySource)
        {
            _httpProxyAuthentication = httpProxyAuthentication;
        }

        public override async Task<bool> IsAcceptUserAsync(IUserInfo userInfo, CancellationToken cancellationToken = default)
        {
            if (userInfo.Authentication is HttpProxyAuthentication httpProxyAuthentication)
            {
                return httpProxyAuthentication.Equals(_httpProxyAuthentication);
            }
            return false;
        }
    }
}
