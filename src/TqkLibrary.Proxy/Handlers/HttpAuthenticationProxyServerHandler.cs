using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Handlers
{
    public class HttpAuthenticationProxyServerHandler : HttpProxyServerHandler
    {
        readonly HttpProxyServerHandler? _parent;
        public HttpAuthenticationProxyServerHandler()
        {

        }
        public HttpAuthenticationProxyServerHandler(IProxySource proxySource) : base(proxySource)
        {

        }
        public HttpAuthenticationProxyServerHandler(HttpProxyServerHandler parent) : base(parent)
        {
            this._parent = parent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<bool> IsNeedAuthenticationAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_httpProxyAuthentications.Any());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="httpProxyAuthentication"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override Task<bool> CheckAuthenticationAsync(
            HttpProxyAuthentication httpProxyAuthentication,
            CancellationToken cancellationToken = default
            )
        {
            return Task.FromResult(_CheckAuthenticationAsync(httpProxyAuthentication, cancellationToken));
        }


        /*=========================================================================*/



        protected readonly List<HttpProxyAuthentication> _httpProxyAuthentications = new List<HttpProxyAuthentication>();
        public virtual HttpProxyServerHandler WithAuthentications(params HttpProxyAuthentication[] httpProxyAuthentications)
            => this.WithAuthentications(httpProxyAuthentications?.AsEnumerable());
        public virtual HttpProxyServerHandler WithAuthentications(IEnumerable<HttpProxyAuthentication>? httpProxyAuthentications)
        {
            if (httpProxyAuthentications is null) throw new ArgumentNullException(nameof(httpProxyAuthentications));
            _httpProxyAuthentications.AddRange(httpProxyAuthentications.Where(x => x is not null));
            return this;
        }

        bool _CheckAuthenticationAsync(HttpProxyAuthentication httpProxyAuthentication, CancellationToken cancellationToken = default)
        {
            if (_httpProxyAuthentications.Any())
            {
                if (httpProxyAuthentication is null)
                    return false;

                if (_httpProxyAuthentications.Any(x => x?.Equals(httpProxyAuthentication) == true))
                    return true;

                return false;
            }
            else
            {
                return true;
            }
        }
    }
}
