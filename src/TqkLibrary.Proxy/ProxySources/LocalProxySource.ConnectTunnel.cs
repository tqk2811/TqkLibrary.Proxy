using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;
using System.Threading;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class ConnectTunnel : BaseProxySourceTunnel<LocalProxySource>, IConnectSource
        {
            readonly TcpClient _tcpClient = new TcpClient();
            Stream? _stream = null;
            public ConnectTunnel(LocalProxySource localProxySource) : base(localProxySource)
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
                if (address is null)
                    throw new ArgumentNullException(nameof(address));
                CheckIsDisposed();

                switch (address.HostNameType)
                {
                    case UriHostNameType.Dns://http://host/abc/def
                    case UriHostNameType.IPv4:
                    case UriHostNameType.IPv6:
                        {
                            switch (address.Scheme.ToLower())
                            {
                                case "http":// http proxy
                                case "https":

                                case "ws":// for http request (not CONNECT)
                                case "wss":

                                case "tcp":// socks4 / socks5
                                    {
#if NET5_0_OR_GREATER
                                        await _tcpClient.ConnectAsync(address.Host, address.Port, cancellationToken);
#else
                                        await _tcpClient.ConnectAsync(address.Host, address.Port);
#endif
                                        _stream = _tcpClient.GetStream();
                                    }
                                    break;

                                default:
                                    throw new NotSupportedException(address.Scheme);
                            }
                        }
                        break;

                    default:
                        throw new NotSupportedException(address.HostNameType.ToString());
                }
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
