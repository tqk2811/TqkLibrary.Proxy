using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;

namespace TestProxy
{
    public abstract class BaseClassTest : IDisposable
    {
        protected readonly ProxyServer _proxyServer;
        protected BaseClassTest()
        {
            _proxyServer = CreateServer(GetProxySource());
            _proxyServer.StartListen();
        }
        ~BaseClassTest()
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
            _proxyServer.Dispose();
        }
        protected abstract IProxySource GetProxySource();
        protected abstract ProxyServer CreateServer(IProxySource proxySource);
    }
}
