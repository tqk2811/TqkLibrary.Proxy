using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    internal abstract class BaseProxySourceTunnel<T> : BaseLogger, IDisposable
        where T : class, IProxySource
    {
        protected readonly T _proxySource;
        private bool _IsDisposed = false;
        protected BaseProxySourceTunnel(T proxySource)
        {
            _proxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
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
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}
