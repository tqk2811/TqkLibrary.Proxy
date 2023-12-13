using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class ConnectTunnel : BaseTunnel, IConnectSource
        {
            internal ConnectTunnel(
                Socks5ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }

            public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(this._stream);
            }

            internal async Task<IConnectSource> InitConnectAsync(Uri address)
            {
                try
                {
                    await InitAsync();
                    if (await _ConnectionRequestAsync(address))
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

            async Task<bool> _ConnectionRequestAsync(Uri address)
            {
                Socks5_Request socks5_Connection = new Socks5_Request(Socks5_CMD.EstablishStreamConnection, address);
                await _stream.WriteAsync(socks5_Connection.GetByteArray(), _cancellationToken);
                await _stream.FlushAsync(_cancellationToken);

                Socks5_RequestResponse socks5_RequestResponse = await _stream.Read_Socks5_RequestResponse_Async(_cancellationToken);
                if (socks5_RequestResponse.STATUS == Socks5_STATUS.RequestGranted)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
    }
}
