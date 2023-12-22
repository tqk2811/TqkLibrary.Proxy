using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Handlers
{
    public class Socks4ProxyServerHandler : BaseProxyServerHandler
    {
        const bool _IsSupportSock4A = false;
        readonly Socks4ProxyServerHandler? _parent;
        public Socks4ProxyServerHandler()
        {

        }
        public Socks4ProxyServerHandler(Socks4ProxyServerHandler parent) : base(parent)
        {
            this._parent = parent;
        }

        public virtual Task<bool> IsUseSocks4AAsync(CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.IsUseSocks4AAsync();
            else return Task.FromResult(_IsSupportSock4A);
        }
    }
}
