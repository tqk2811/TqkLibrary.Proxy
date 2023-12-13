using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class UdpTunnel : BaseTunnel, IUdpAssociateSource
        {
            internal UdpTunnel(
                LocalProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }


            internal async Task<IUdpAssociateSource> InitUdpAsync(Uri address)
            {
                return this;
            }
        }
    }
}
