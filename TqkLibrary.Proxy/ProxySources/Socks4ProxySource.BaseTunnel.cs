using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        class BaseTunnel : BaseProxySourceTunnel<Socks4ProxySource>
        {
            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream _stream;

            internal BaseTunnel(
                Socks4ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            protected async Task _ConnectToSocksServerAsync()
            {
                await _tcpClient.ConnectAsync(_proxySource.iPEndPoint.Address, _proxySource.iPEndPoint.Port);
                this._stream = _tcpClient.GetStream();
            }

        }
    }
}
