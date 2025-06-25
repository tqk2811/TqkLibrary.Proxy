using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.GlobalUnicast
{
    public partial class GlobalUnicastProxySource
    {
        class ConnectTunnel : BaseTunnel, IConnectSource
        {
            protected readonly TcpClient _tcpClient = new TcpClient(AddressFamily.InterNetworkV6);
            protected Stream? _stream = null;
            public ConnectTunnel(GlobalUnicastProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _stream = null;
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            public async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (_proxySource.AssignAddress is null)
                    throw new InvalidOperationException($"{nameof(GlobalUnicastProxySource)}.{nameof(_proxySource.AssignAddress)} was not init or disposed");

                var endPoint = new IPEndPoint(_proxySource.AssignAddress, 0);
                _tcpClient.Client.Bind(endPoint);
                await _tcpClient.ConnectAsync(address.Host, address.Port);
                _stream = _tcpClient.GetStream();
            }

            public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(_stream);
            }
        }

    }
}
