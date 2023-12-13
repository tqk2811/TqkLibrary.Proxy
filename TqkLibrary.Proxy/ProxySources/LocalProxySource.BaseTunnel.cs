namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class BaseTunnel : BaseProxySourceTunnel<LocalProxySource>
        {
            internal BaseTunnel(
                LocalProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }
        }
    }
}
