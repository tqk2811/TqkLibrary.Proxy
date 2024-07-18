using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class ConnectTunnel : BaseTunnel, IConnectSource
        {
            internal ConnectTunnel(Socks5ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }

            public async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));
                CheckIsDisposed();

                await base.ConnectAndAuthAsync(cancellationToken);

                Socks5_Request socks5_Connection = Socks5_Request.CreateConnect(address);
                await _stream!.WriteAsync(socks5_Connection.GetByteArray(), cancellationToken);
                await _stream!.FlushAsync(cancellationToken);
                Socks5_RequestResponse socks5_RequestResponse = await _stream!.Read_Socks5_RequestResponse_Async(cancellationToken);
                if (socks5_RequestResponse.STATUS != Socks5_STATUS.RequestGranted)
                {
                    throw new InitConnectSourceFailedException($"{nameof(Socks5_STATUS)}: {socks5_RequestResponse.STATUS}");
                }
            }
            public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectTunnel)}.{nameof(ConnectAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(_stream);
            }
        }
    }
}
