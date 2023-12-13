using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Filters
{
    public class Socks4ProxyServerFilter : BaseProxyServerFilter
    {
        const bool _IsSupportSock4A = false;
        readonly Socks4ProxyServerFilter _parent;
        public Socks4ProxyServerFilter()
        {

        }
        public Socks4ProxyServerFilter(Socks4ProxyServerFilter parent) : base(parent)
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
