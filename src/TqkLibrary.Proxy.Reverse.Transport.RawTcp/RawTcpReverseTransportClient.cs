using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Transport.RawTcp
{
    public sealed class RawTcpReverseTransportClient : IReverseTransportClient
    {
        private readonly EndPoint _endpoint;
        private readonly RawTcpTlsOptions? _tls;

        public RawTcpReverseTransportClient(EndPoint endpoint, RawTcpTlsOptions? tls = null)
        {
            _endpoint = endpoint;
            _tls = tls;
        }

        public async Task<IControlChannel> ConnectControlAsync(CancellationToken cancellationToken = default)
        {
            var (stream, remote) = await DialAsync(RawTcpReverseTransportServer.ChannelControl, cancellationToken).ConfigureAwait(false);
            return new StreamControlChannel(stream, remote);
        }

        public async Task<Stream> OpenDataAsync(CancellationToken cancellationToken = default)
        {
            var (stream, _) = await DialAsync(RawTcpReverseTransportServer.ChannelData, cancellationToken).ConfigureAwait(false);
            return stream;
        }

        private async Task<(Stream stream, string remote)> DialAsync(byte kind, CancellationToken ct)
        {
            var tcp = new TcpClient { NoDelay = true };
            try
            {
#if NET6_0_OR_GREATER
                if (_endpoint is DnsEndPoint dns)
                    await tcp.ConnectAsync(dns.Host, dns.Port, ct).ConfigureAwait(false);
                else if (_endpoint is IPEndPoint ip)
                    await tcp.ConnectAsync(ip.Address, ip.Port, ct).ConfigureAwait(false);
                else
                    throw new NotSupportedException($"Endpoint type {_endpoint.GetType()} not supported");
#else
                using (ct.Register(() => tcp.Dispose()))
                {
                    if (_endpoint is DnsEndPoint dns)
                        await tcp.ConnectAsync(dns.Host, dns.Port).ConfigureAwait(false);
                    else if (_endpoint is IPEndPoint ip)
                        await tcp.ConnectAsync(ip.Address, ip.Port).ConfigureAwait(false);
                    else
                        throw new NotSupportedException($"Endpoint type {_endpoint.GetType()} not supported");
                }
#endif
                Stream stream = tcp.GetStream();
                string remote = tcp.Client.RemoteEndPoint?.ToString() ?? "?";

                if (_tls is not null)
                {
                    var ssl = new SslStream(stream, false, _tls.ValidateServerCertificate);
                    string host = _tls.TargetHost
                        ?? (_endpoint is DnsEndPoint d ? d.Host : (_endpoint as IPEndPoint)?.Address.ToString() ?? "");
#if NET6_0_OR_GREATER
                    var opts = new SslClientAuthenticationOptions
                    {
                        TargetHost = host,
                        ClientCertificates = _tls.ClientCertificates,
                        EnabledSslProtocols = _tls.Protocols,
                        RemoteCertificateValidationCallback = _tls.ValidateServerCertificate,
                    };
                    await ssl.AuthenticateAsClientAsync(opts, ct).ConfigureAwait(false);
#else
                    await ssl.AuthenticateAsClientAsync(host, _tls.ClientCertificates, _tls.Protocols, false)
                        .ConfigureAwait(false);
#endif
                    stream = ssl;
                }

#if NET6_0_OR_GREATER
                await stream.WriteAsync(new byte[] { kind }, ct).ConfigureAwait(false);
#else
                await stream.WriteAsync(new byte[] { kind }, 0, 1, ct).ConfigureAwait(false);
#endif
                await stream.FlushAsync(ct).ConfigureAwait(false);
                return (stream, remote);
            }
            catch
            {
                tcp.Dispose();
                throw;
            }
        }
    }
}
