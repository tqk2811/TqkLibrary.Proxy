using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource
    {
        class Socks4ProxySourceTunnel : BaseProxySourceTunnel<HttpProxySource>, IConnectSource
        {
            internal Socks4ProxySourceTunnel(
                HttpProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }

            public Stream GetStream()
            {
                return this._stream;
            }

            public async Task<IConnectSource> InitConnectAsync(Uri address)
            {
                if (address is null) throw new ArgumentNullException(nameof(address));
                await ConnectToProxy();
                if (await CONNECT(address))
                {
                    return this;
                }
                this.Dispose();
                return null;
            }

            async Task ConnectToProxy()
            {
                await _tcpClient.ConnectAsync(_proxySource.proxy.Host, _proxySource.proxy.Port);
                _stream = _tcpClient.GetStream();
            }

            async Task<bool> CONNECT(Uri address)
            {
                await _stream.WriteLineAsync($"CONNECT {address.Host}:{address.Port} HTTP/1.1");
#if DEBUG
                Console.WriteLine($"[{nameof(Socks4ProxySourceTunnel)}.{nameof(CONNECT)}] {_proxySource.proxy.Host}:{_proxySource.proxy.Port} <- CONNECT {address.Host}:{address.Port} HTTP/1.1");
#endif
                if (_proxySource.networkCredential != null)
                {
                    string data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_proxySource.networkCredential.UserName}:{_proxySource.networkCredential.Password}"));
                    await _stream.WriteLineAsync($"Proxy-Authorization: Basic {data}");
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks4ProxySourceTunnel)}.{nameof(CONNECT)}] {_proxySource.proxy.Host}:{_proxySource.proxy.Port} <- Proxy-Authorization: Basic {data}");
#endif
                }
                await _stream.WriteLineAsync();
                await _stream.FlushAsync();


                List<string> response_HeaderLines = await _stream.ReadHeader();
#if DEBUG
                response_HeaderLines.ForEach(x =>
                    Console.WriteLine($"[{nameof(Socks4ProxySourceTunnel)}.{nameof(CONNECT)}] {_proxySource.proxy.Host}:{_proxySource.proxy.Port} -> {x}"));
#endif
                var headerResponseParse = response_HeaderLines.ParseResponse();

                return headerResponseParse.HttpStatusCode == HttpStatusCode.OK;
            }
        }
    }
}
