using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Handlers
{
    public class BasePreProxyServerHandler : IPreProxyServerHandler
    {
        public virtual Task<bool> IsAcceptClientAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }
        public virtual Task<Stream> StreamHandlerAsync(Stream stream, IPEndPoint iPEndPoint, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(stream);
        }

        public virtual async Task<IProxyServer> GetProxyServerAsync(PreReadStream preReadStream, IPEndPoint iPEndPoint, CancellationToken cancellationToken = default)
        {
            byte[] buffer = await preReadStream.PreReadAsync(1, cancellationToken).ConfigureAwait(false);
            if (buffer.Length == 0)
                throw new InvalidOperationException($"Invalid Request");

            switch (buffer[0])
            {
                case 0x04:
                    return new Socks4ProxyServer();

                case 0x05:
                    return new Socks5ProxyServer();

                default:
                    {
                        string header = await preReadStream.PreReadLineAsync(32 * 1024, cancellationToken);
                        if (header.Contains("HTTP/", StringComparison.OrdinalIgnoreCase))
                        {
                            return new HttpProxyServer();
                        }
                        else throw new InvalidOperationException($"Invalid Request");
                    }
            }
        }
    }
}
