using System.Net;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        class BindTunnel : BaseTunnel, IBindSource
        {
            internal BindTunnel(
                Socks4ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }


            Socks4_RequestResponse _socks4_RequestResponse;
            internal async Task<IBindSource> InitBindAsync(Uri address)
            {
                try
                {
                    await _ConnectToSocksServerAsync();

                    Socks4_Request socks4_Request = new Socks4_Request(Socks4_CMD.Bind, address, _proxySource.userId);

                    byte[] buffer = socks4_Request.GetByteArray();
                    await this._stream.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);

                    _socks4_RequestResponse = await this._stream.Read_Socks4_RequestResponse_Async(_cancellationToken);
                    if (_socks4_RequestResponse.REP == Socks4_REP.RequestGranted)
                    {
                        return this;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(ConnectTunnel)}.{nameof(InitBindAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }


            public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_socks4_RequestResponse.IPEndPoint);
            }
            public Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_stream);
            }
        }
    }
}
