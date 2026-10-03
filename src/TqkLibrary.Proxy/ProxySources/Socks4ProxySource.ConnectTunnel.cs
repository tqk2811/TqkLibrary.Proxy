using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        public class ConnectTunnel : BaseTunnel, IConnectSource
        {
            internal protected ConnectTunnel(Socks4ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }
            public virtual async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));

                await base._ConnectToSocksServerAsync(cancellationToken);

                Socks4_Request socks4_Request = Socks4_Request.CreateConnect(address, _proxySource.userId);
                byte[] buffer = socks4_Request.GetByteArray();

                if (_proxySource.ConnectRequestChunkSize > 0)
                {
                    // NoDelay keeps the stack from coalescing the small chunks; restored so the
                    // tunnelled data is sent the way it always was.
                    bool noDelay = _tcpClient.NoDelay;
                    _tcpClient.NoDelay = true;
                    try
                    {
                        // Only the destination is trickled out: the domain of SOCKS4a (after
                        // USERID\0, up to its own \0), or the IPv4 address of plain SOCKS4.
                        int addressStart, addressLength;
                        if (socks4_Request.IsDomain)
                        {
                            addressStart = Array.IndexOf(buffer, (byte)0, 8) + 1;
                            addressLength = buffer.Length - addressStart - 1;
                        }
                        else
                        {
                            addressStart = 4;
                            addressLength = 4;
                        }
                        await base._stream!.WriteSplitAroundAsync(buffer, 0, buffer.Length, addressStart, addressLength, _proxySource.ConnectRequestChunkSize, cancellationToken);
                    }
                    finally
                    {
                        _tcpClient.NoDelay = noDelay;
                    }
                }
                else
                {
                    await base._stream!.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
                }

                Socks4_RequestResponse socks4_RequestResponse = await base._stream!.Read_Socks4_RequestResponse_Async(cancellationToken);
                if (socks4_RequestResponse.REP != Socks4_REP.RequestGranted)
                {
                    throw new InitConnectSourceFailedException($"{nameof(Socks4_REP)}: {socks4_RequestResponse.REP}");
                }

                // Wrapped only once CONNECT is through: the request above is the proxy's business,
                // the bytes from here on are the tunnel's.
                if (_proxySource.TlsHandshakeChunkSize > 0)
                    _stream = new TlsHandshakeChunkingStream(_stream!, _proxySource.TlsHandshakeChunkSize, _tcpClient.Client);
            }


            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectTunnel)}.{nameof(ConnectAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(_stream);
            }
        }
    }
}
