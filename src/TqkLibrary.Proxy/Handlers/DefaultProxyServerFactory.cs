using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.Handlers
{
    public class DefaultProxyServerFactory : IProxyServerFactory
    {
        readonly ILoggerFactory? _loggerFactory;

        public DefaultProxyServerFactory(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
        }

        public async Task<IProxyServer> CreateAsync(PreReadStream preReadStream, CancellationToken cancellationToken = default)
        {
            byte[] buffer = await preReadStream.PreReadAsync(1, cancellationToken).ConfigureAwait(false);
            if (buffer.Length == 0)
                throw new InvalidOperationException("Invalid Request");

            switch (buffer[0])
            {
                case 0x04:
                    return new Socks4ProxyServer(_loggerFactory);

                case 0x05:
                    return new Socks5ProxyServer(_loggerFactory);

                default:
                    string header = await preReadStream.PreReadLineAsync(32 * 1024, cancellationToken).ConfigureAwait(false);
                    if (header.Contains("HTTP/", StringComparison.OrdinalIgnoreCase))
                        return new HttpProxyServer(_loggerFactory);
                    throw new InvalidOperationException("Invalid Request");
            }
        }
    }
}
