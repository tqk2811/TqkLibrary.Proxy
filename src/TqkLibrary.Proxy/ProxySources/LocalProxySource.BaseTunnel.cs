using Microsoft.Extensions.Logging;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        public class BaseTunnel : BaseProxySourceTunnel<LocalProxySource>
        {
            protected readonly ILogger? _logger;

            internal protected BaseTunnel(LocalProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {
                _logger = proxySource._loggerFactory?.CreateLogger(GetType());
            }
        }
    }
}
