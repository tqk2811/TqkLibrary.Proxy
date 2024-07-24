namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        public class BaseTunnel : BaseProxySourceTunnel<LocalProxySource>
        {
            internal protected BaseTunnel(LocalProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }
        }
    }
}
