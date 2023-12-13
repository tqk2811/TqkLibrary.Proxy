using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class UdpTunnel : BaseTunnel, IUdpAssociateSource
        {
            internal UdpTunnel(
                Socks5ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }


            public async Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address)
            {
                try
                {
                    await InitAsync();



                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(ConnectTunnel)}.{nameof(InitUdpAssociateAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }
        }
    }
}
