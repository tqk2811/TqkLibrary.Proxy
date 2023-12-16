using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Authentications;

namespace TqkLibrary.Proxy.Filters
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseProxyServerFilter
    {
        readonly BaseProxyServerFilter? _parent;
        public BaseProxyServerFilter()
        {

        }
        public BaseProxyServerFilter(BaseProxyServerFilter parent)
        {
            this._parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> IsAcceptClientFilterAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.IsAcceptClientFilterAsync(tcpClient, cancellationToken);
            else return Task.FromResult(true);
        }

        /// <summary>
        /// SSL or encrypt ......
        /// </summary>
        /// <param name="networkStream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<Stream> StreamFilterAsync(Stream networkStream, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.StreamFilterAsync(networkStream, cancellationToken);
            else return Task.FromResult(networkStream);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> IsAcceptDomainFilterAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.IsAcceptDomainFilterAsync(uri, cancellationToken);
            else return Task.FromResult(true);
        }
    }
}
