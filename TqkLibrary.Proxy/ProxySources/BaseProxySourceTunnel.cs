using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    internal abstract class BaseProxySourceTunnel<T> : IDisposable
        where T : IProxySource
    {
        protected readonly T _proxySource;
        protected readonly CancellationToken _cancellationToken;
        protected BaseProxySourceTunnel(T proxySource, CancellationToken cancellationToken = default)
        {
            this._proxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
            this._cancellationToken = cancellationToken;
        }
        ~BaseProxySourceTunnel()
        {
            Dispose(false);
        }
        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool isDisposing)
        {

        }
    }
}
