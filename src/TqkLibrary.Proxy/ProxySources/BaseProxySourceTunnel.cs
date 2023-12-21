using Microsoft.Extensions.Logging;
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
        where T : class, IProxySource
    {
        protected readonly ILogger? _logger;
        protected readonly T _proxySource;
        private bool _IsDisposed = false;
        protected BaseProxySourceTunnel(T proxySource)
        {
            this._proxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
            _logger = Singleton.LoggerFactory?.CreateLogger(this.GetType());
        }
        ~BaseProxySourceTunnel()
        {
            Dispose(false);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool isDisposing)
        {
            _IsDisposed = true;
        }

        protected void CheckIsDisposed()
        {
            if (_IsDisposed)
                throw new ObjectDisposedException(this.GetType().FullName);
        }
    }
}
