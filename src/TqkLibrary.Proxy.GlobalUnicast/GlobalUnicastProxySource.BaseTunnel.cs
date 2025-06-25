using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;

namespace TqkLibrary.Proxy.GlobalUnicast
{
    public partial class GlobalUnicastProxySource
    {
        public class BaseTunnel : BaseProxySourceTunnel<GlobalUnicastProxySource>
        {
            public BaseTunnel(GlobalUnicastProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {
            }


        }

    }
}
