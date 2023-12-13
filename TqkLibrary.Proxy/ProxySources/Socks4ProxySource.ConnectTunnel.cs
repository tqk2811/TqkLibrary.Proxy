using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        class ConnectTunnel : BaseTunnel, IConnectSource
        {
            internal ConnectTunnel(
                Socks4ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }

            internal async Task<IConnectSource> InitConnectAsync(Uri address)
            {
                try
                {
                    await base._ConnectToSocksServerAsync();

                    Socks4_Request socks4_Request = new Socks4_Request(Socks4_CMD.Connect, address, _proxySource.userId);

                    byte[] buffer = socks4_Request.GetByteArray();
                    await base._stream.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);

                    Socks4_RequestResponse socks4_RequestResponse = await base._stream.Read_Socks4_RequestResponse_Async(_cancellationToken);
                    if (socks4_RequestResponse.REP == Socks4_REP.RequestGranted)
                    {
                        return this;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(ConnectTunnel)}.{nameof(InitConnectAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }

            public Stream GetStream()
            {
                return this._stream;
            }
        }
    }
}
