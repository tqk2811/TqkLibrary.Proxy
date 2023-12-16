using TqkLibrary.Proxy.Authentications;

namespace TqkLibrary.Proxy.Filters
{
    /// <summary>
    /// 
    /// </summary>
    public class HttpProxyServerFilter : BaseProxyServerFilter
    {
        readonly HttpProxyServerFilter? _parent;
        public HttpProxyServerFilter()
        {

        }
        public HttpProxyServerFilter(HttpProxyServerFilter parent) : base(parent)
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
