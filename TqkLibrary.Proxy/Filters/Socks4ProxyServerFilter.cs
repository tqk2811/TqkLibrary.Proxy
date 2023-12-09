using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Filters
{
    public class Socks4ProxyServerFilter : BaseProxyServerFilter
    {


        public bool IsUseSocks4A { get; set; } = true;
        public virtual Task<bool> IsUseSocks4AAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsUseSocks4A);
        }
    }
}
