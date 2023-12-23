using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Handlers
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpProxyServerHandler : BaseProxyServerHandler
    {
        readonly HttpProxyServerHandler? _parent;
        public HttpProxyServerHandler()
        {

        }
        public HttpProxyServerHandler(IProxySource proxySource) : base(proxySource)
        {

        }
        public HttpProxyServerHandler(HttpProxyServerHandler parent) : base(parent)
        {
            this._parent = parent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> IsNeedAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.IsNeedAuthenticationAsync(cancellationToken);
            else return Task.FromResult(false);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpProxyAuthentication"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> CheckAuthenticationAsync(
            HttpProxyAuthentication httpProxyAuthentication,
            CancellationToken cancellationToken = default
            )
        {
            if (_parent is not null) return _parent.CheckAuthenticationAsync(httpProxyAuthentication, cancellationToken);
            else return Task.FromResult(true);
        }
    }
}
