using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
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
#if NET5_0_OR_GREATER
                await _tcpClient.ConnectAsync(_proxySource._proxy.Host, _proxySource._proxy.Port, cancellationToken);
#else
                await _tcpClient.ConnectAsync(_proxySource._proxy.Host, _proxySource._proxy.Port);
#endif
                _stream = _tcpClient.GetStream();

                if (!await _CONNECT_Async(address, cancellationToken))
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

            async Task<bool> _CONNECT_Async(Uri address, CancellationToken cancellationToken = default)
            {
                if (this._stream is null)
                    throw new InvalidOperationException();

                List<string> headers = new List<string>();
                headers.Add($"CONNECT {address.Host}:{address.Port} HTTP/1.1");
                if (_proxySource.HttpProxyAuthentication is not null)
                {
                    string data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_proxySource.HttpProxyAuthentication.UserName}:{_proxySource.HttpProxyAuthentication.Password}"));
                    headers.Add($"Proxy-Authorization: Basic {data}");
                }

                await _stream.WriteHeadersAsync(headers, cancellationToken);
                _logger?.LogInformation($"{_proxySource._proxy.Host}:{_proxySource._proxy.Port} <-\r\n{string.Join("\r\n", headers)}");

                await _stream.FlushAsync(cancellationToken);

                //-----------------------///

                IReadOnlyList<string> response_HeaderLines = await _stream.ReadHeadersAsync(cancellationToken);
                _logger?.LogInformation($"{_proxySource._proxy.Host}:{_proxySource._proxy.Port} ->\r\n{string.Join("\r\n", response_HeaderLines)}");

                var headerResponseParse = HeaderResponseParse.ParseResponse(response_HeaderLines);

                return headerResponseParse.HttpStatusCode == HttpStatusCode.OK;
            }

        }
    }
}
