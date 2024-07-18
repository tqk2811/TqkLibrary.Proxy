namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class BaseTunnel : BaseProxySourceTunnel<LocalProxySource>
        {
            internal BaseTunnel(LocalProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }
        }
    }
}
