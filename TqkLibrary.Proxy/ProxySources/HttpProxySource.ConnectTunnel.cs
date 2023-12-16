using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class HttpProxySource
    {
        class ConnectTunnel : BaseProxySourceTunnel<HttpProxySource>, IConnectSource
        {
            readonly TcpClient _tcpClient = new TcpClient();
            Stream? _stream;
            internal ConnectTunnel(HttpProxySource proxySource) : base(proxySource)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _stream = null;
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }

            public async Task InitAsync(Uri address, CancellationToken cancellationToken = default)
            {
                if (address is null)
                    throw new ArgumentNullException(nameof(address));
                CheckIsDisposed();

                await _tcpClient.ConnectAsync(_proxySource._proxy.Host, _proxySource._proxy.Port);
                _stream = _tcpClient.GetStream();

                if (!await _CONNECT_Async(address))
                {
                    throw new InitConnectSourceFailedException();
                }
            }
            public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (this._stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(ConnectTunnel)}.{nameof(InitAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(this._stream);
            }

            async Task<bool> _CONNECT_Async(Uri address)
            {
                if (this._stream is null)
                    throw new InvalidOperationException();

                await _stream.WriteLineAsync($"CONNECT {address.Host}:{address.Port} HTTP/1.1");
#if DEBUG
                Console.WriteLine($"[{nameof(ConnectTunnel)}.{nameof(_CONNECT_Async)}] {_proxySource._proxy.Host}:{_proxySource._proxy.Port} <- CONNECT {address.Host}:{address.Port} HTTP/1.1");
#endif
                if (_proxySource.HttpProxyAuthentication is not null)
                {
                    string data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_proxySource.HttpProxyAuthentication.UserName}:{_proxySource.HttpProxyAuthentication.Password}"));
                    await _stream.WriteLineAsync($"Proxy-Authorization: Basic {data}");
#if DEBUG
                    Console.WriteLine($"[{nameof(ConnectTunnel)}.{nameof(_CONNECT_Async)}] {_proxySource._proxy.Host}:{_proxySource._proxy.Port} <- Proxy-Authorization: Basic {data}");
#endif
                }
                await _stream.WriteLineAsync();
                await _stream.FlushAsync();


                List<string> response_HeaderLines = await _stream.ReadHeader();
#if DEBUG
                response_HeaderLines.ForEach(x =>
                    Console.WriteLine($"[{nameof(ConnectTunnel)}.{nameof(_CONNECT_Async)}] {_proxySource._proxy.Host}:{_proxySource._proxy.Port} -> {x}"));
#endif
                var headerResponseParse = response_HeaderLines.ParseResponse();

                return headerResponseParse.HttpStatusCode == HttpStatusCode.OK;
            }

        }
    }
}
