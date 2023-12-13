using System.Net;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class BindTunnel : BaseTunnel, IBindSource
        {
            internal BindTunnel(
                Socks5ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }


            internal async Task<IBindSource> InitBindAsync(Uri address)
            {
                try
                {
                    await InitAsync();
                    if (await BindRequestAsync(address))
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

            Socks5_RequestResponse _socks5_RequestResponse = null;
            async Task<bool> BindRequestAsync(Uri address)
            {
                if (_proxySource.IsSupportBind)
                {
                    Socks5_Request socks5_Connection = new Socks5_Request(Socks5_CMD.EstablishPortBinding, address);
                    await _stream.WriteAsync(socks5_Connection.GetByteArray(), _cancellationToken);
                    await _stream.FlushAsync(_cancellationToken);

                    _socks5_RequestResponse = await _stream.Read_Socks5_RequestResponse_Async(_cancellationToken);
                    if (_socks5_RequestResponse.STATUS == Socks5_STATUS.RequestGranted)
                    {
                        return true;
                    }
                }

                return false;
            }

            public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_socks5_RequestResponse.IPEndPoint);
            }

            public Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_stream);
            }
        }
    }
}
