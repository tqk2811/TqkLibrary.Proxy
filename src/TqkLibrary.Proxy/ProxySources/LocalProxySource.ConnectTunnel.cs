using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;

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

            static readonly IEnumerable<string> _SupportUriSchemes = new string[]
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
