using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TqkLibrary.Proxy.Handlers
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseProxyServerHandler
    {
        readonly BaseProxyServerHandler? _parent;
        readonly IProxySource? _proxySource;
        LocalProxySource? _localProxySource;
        public BaseProxyServerHandler()
        {

        }
        public BaseProxyServerHandler(IProxySource proxySource)
        {
            this._proxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
        }
        public BaseProxyServerHandler(BaseProxyServerHandler parent)
        {
            this._parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }


        public virtual Task<IProxySource> GetProxySourceAsync(CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.GetProxySourceAsync(cancellationToken);
            else
            {
                if (_proxySource is not null)
                    return Task.FromResult(_proxySource);

                if (_localProxySource is null)
                    _localProxySource = new LocalProxySource();

                return Task.FromResult<IProxySource>(_localProxySource);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> IsAcceptClientAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.IsAcceptClientAsync(tcpClient, cancellationToken);
            else return Task.FromResult(true);
        }

        /// <summary>
        /// SSL or encrypt ......
        /// </summary>
        /// <param name="networkStream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<Stream> StreamHandlerAsync(Stream networkStream, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.StreamHandlerAsync(networkStream, cancellationToken);
            else return Task.FromResult(networkStream);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> IsAcceptDomainAsync(Uri uri, CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.IsAcceptDomainAsync(uri, cancellationToken);
            else
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
                        if (uri.Host.StartsWith("127.", StringComparison.OrdinalIgnoreCase))
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
        }
    }
}
