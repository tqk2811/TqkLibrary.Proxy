using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        public class ConnectTunnel : BaseProxySourceTunnel<LocalProxySource>, IConnectSource
        {
            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream? _stream = null;
            internal protected ConnectTunnel(LocalProxySource localProxySource, Guid tunnelId) : base(localProxySource, tunnelId)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _stream = null;
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            static protected readonly IEnumerable<string> _SupportUriSchemes = new string[]
            {
                Uri.UriSchemeHttp,
                Uri.UriSchemeHttps,
                Uri.UriSchemeFtp,
                Uri.UriSchemeNetTcp,
                Uri.UriSchemeNetPipe,
                Uri.UriSchemeNews,
                Uri.UriSchemeNntp,
                Uri.UriSchemeFile,
                Uri.UriSchemeGopher,
                Uri.UriSchemeMailto,
                "tcp",
#if NET6_0_OR_GREATER
                Uri.UriSchemeWs,
                Uri.UriSchemeWss,
                Uri.UriSchemeFtps,
                Uri.UriSchemeSsh,
                Uri.UriSchemeTelnet,
                Uri.UriSchemeSftp,
                Uri.UriSchemeNntp,
#else
                "ws",
                "wss",
                "ftps",
                "ssh",
                "telnet",
                "sftp",
                "nntp",
#endif
            };

            public virtual async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));
                CheckIsDisposed();

                switch (address.HostNameType)
                {
                    case UriHostNameType.Dns://http://host/abc/def
                        {
                            var ips = await Dns.GetHostAddressesAsync(address.Host);
                            if (!_proxySource.IsSupportIpv6)
                            {
                                ips = ips.Where(x => x.AddressFamily == AddressFamily.InterNetwork).ToArray();//ipv4 only
                            }
                            else if (_proxySource.IsPrioritizeIpv4.HasValue)
                            {
                                //prioritize ip v4/v6
                                var order = _proxySource.IsPrioritizeIpv4.Value ? ips.OrderBy(x => x.AddressFamily) : ips.OrderByDescending(x => x.AddressFamily);
                                ips = order.ToArray();
                            }
                            await _tcpClient.ConnectAsync(
                                   ips,
                                   address.Port
#if NET5_0_OR_GREATER
                                    , cancellationToken
#endif
                                );
                            _stream = _tcpClient.GetStream();
                            break;
                        }

                    case UriHostNameType.IPv4:
                    case UriHostNameType.IPv6:
                        {
                            if (!_proxySource.IsSupportIpv6 && address.HostNameType == UriHostNameType.IPv6)
                                throw new NotSupportedException($"IpV6 are not support");

                            if (_SupportUriSchemes.Any(x => x.Equals(address.Scheme, StringComparison.InvariantCulture)))
                            {
                                await _tcpClient.ConnectAsync(
                                    address.Host,
                                    address.Port
#if NET5_0_OR_GREATER
                                    , cancellationToken
#endif
                                );
                                _stream = _tcpClient.GetStream();
                            }
                            else
                            {
                                throw new NotSupportedException(address.Scheme);
                            }
                        }
                        break;

                    default:
                        throw new NotSupportedException(address.HostNameType.ToString());
                }
            }

            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(_stream);
            }
        }
    }
}
