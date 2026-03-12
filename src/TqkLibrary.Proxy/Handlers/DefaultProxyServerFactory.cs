using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.Handlers
{
    public class DefaultProxyServerFactory : IProxyServerFactory
    {
        public async Task<IProxyServer> CreateAsync(PreReadStream preReadStream, CancellationToken cancellationToken = default)
        {
            byte[] buffer = await preReadStream.PreReadAsync(1, cancellationToken).ConfigureAwait(false);
            if (buffer.Length == 0)
                throw new InvalidOperationException("Invalid Request");

            switch (buffer[0])
            {
                case 0x04:
                    return new Socks4ProxyServer();

                case 0x05:
                    return new Socks5ProxyServer();

                default:
                    string header = await preReadStream.PreReadLineAsync(32 * 1024, cancellationToken).ConfigureAwait(false);
                    if (header.Contains("HTTP/", StringComparison.OrdinalIgnoreCase))
                        return new HttpProxyServer();
                    throw new InvalidOperationException("Invalid Request");
            }
        }
    }
}
