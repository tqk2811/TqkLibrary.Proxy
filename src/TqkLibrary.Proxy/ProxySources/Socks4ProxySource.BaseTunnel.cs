﻿using System.Net.Sockets;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        class BaseTunnel : BaseProxySourceTunnel<Socks4ProxySource>
        {
            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream? _stream;

            internal BaseTunnel(Socks4ProxySource proxySource) : base(proxySource)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            protected async Task _ConnectToSocksServerAsync(CancellationToken cancellationToken = default)
            {
#if NET5_0_OR_GREATER
                await _tcpClient.ConnectAsync(_proxySource.iPEndPoint.Address, _proxySource.iPEndPoint.Port, cancellationToken);
#else
                await _tcpClient.ConnectAsync(_proxySource.iPEndPoint.Address, _proxySource.iPEndPoint.Port);
#endif
                _stream = _tcpClient.GetStream();
            }

        }
    }
}
