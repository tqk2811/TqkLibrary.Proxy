using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class ConnectTunnel : BaseProxySourceTunnel<LocalProxySource>
        {
            readonly Uri _address;
            public ConnectTunnel(
                LocalProxySource localProxySource,
                Uri address,
                CancellationToken cancellationToken = default)
                : base(localProxySource, cancellationToken)
            {
                this._address = address ?? throw new ArgumentNullException(nameof(address));
            }

            public async Task<IConnectSource> InitConnectAsync()
            {
                switch (_address.HostNameType)
                {
                    case UriHostNameType.Dns://http://host/abc/def
                    case UriHostNameType.IPv4:
                    case UriHostNameType.IPv6:
                        {
                            switch (_address.Scheme.ToLower())
                            {
                                case "http":// http proxy
                                case "https":

                                case "ws":// for http request (not CONNECT)
                                case "wss":

                                case "tcp":// socks4 / socks5
                                    {
                                        TcpClient remote = new TcpClient();
                                        try
                                        {
#if NET5_0_OR_GREATER
                                            await remote.ConnectAsync(_address.Host, _address.Port, _cancellationToken);
#else
                                            await remote.ConnectAsync(_address.Host, _address.Port);
#endif
                                            return new TcpStreamConnectSource(remote);
                                        }
                                        catch
                                        {
                                            remote.Dispose();
                                            return null;
                                        }
                                    }

                                default:
                                    throw new NotSupportedException(_address.Scheme);
                            }
                        }

                    default:
                        throw new NotSupportedException(_address.HostNameType.ToString());
                }
            }
        }
    }
}
