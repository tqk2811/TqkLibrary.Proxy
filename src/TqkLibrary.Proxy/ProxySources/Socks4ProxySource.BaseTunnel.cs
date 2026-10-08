using System.Net.Sockets;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        public class BaseTunnel : BaseProxySourceTunnel<Socks4ProxySource>
        {
            // Nagle would hold a game's small writes for up to a delayed-ACK; relay as soon as written.
            protected readonly TcpClient _tcpClient = new TcpClient { NoDelay = true };
            protected Stream? _stream;

            internal protected BaseTunnel(Socks4ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            protected virtual async Task _ConnectToSocksServerAsync(CancellationToken cancellationToken = default)
            {
#if NET5_0_OR_GREATER
                await _tcpClient.ConnectAsync(_proxySource.Uri.DnsSafeHost, _proxySource.Uri.Port, cancellationToken);
#else
                await _tcpClient.ConnectAsync(_proxySource.Uri.DnsSafeHost, _proxySource.Uri.Port);
#endif
                _stream = _tcpClient.GetStream();
            }

        }
    }
}
