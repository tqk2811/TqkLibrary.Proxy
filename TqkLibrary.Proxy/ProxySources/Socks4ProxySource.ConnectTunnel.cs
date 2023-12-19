using System.Threading;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        class ConnectTunnel : BaseTunnel, IConnectSource
        {
            internal ConnectTunnel(Socks4ProxySource proxySource) : base(proxySource)
            {

            }
            public async Task InitAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));

                await base._ConnectToSocksServerAsync(cancellationToken);

                Socks4_Request socks4_Request = new Socks4_Request(Socks4_CMD.Connect, address, _proxySource.userId);
                byte[] buffer = socks4_Request.GetByteArray();

                await base._stream!.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

                Socks4_RequestResponse socks4_RequestResponse = await base._stream!.Read_Socks4_RequestResponse_Async(cancellationToken);
                if (socks4_RequestResponse.REP != Socks4_REP.RequestGranted)
                {
                    throw new InitConnectSourceFailedException($"{nameof(Socks4_REP)}: {socks4_RequestResponse.REP}");
                }
            }


            public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (this._stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectTunnel)}.{nameof(InitAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(this._stream);
            }
        }
    }
}
