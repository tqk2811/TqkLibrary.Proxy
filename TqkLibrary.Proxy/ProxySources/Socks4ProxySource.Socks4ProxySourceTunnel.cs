using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        class Socks4ProxySourceTunnel : BaseProxySourceTunnel<Socks4ProxySource>, IConnectSource, IBindSource
        {
            internal Socks4ProxySourceTunnel(
                Socks4ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }

            #region Connect
            internal async Task<IConnectSource> InitConnectAsync(Uri address)
            {
                try
                {
                    await ConnectToSocksServer();

                    Socks4_Request socks4_Request = new Socks4_Request(Socks4_CMD.Connect, address, _proxySource.userId);

                    byte[] buffer = socks4_Request.GetByteArray();
                    await this._stream.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);

                    Socks4_RequestResponse socks4_RequestResponse = await this._stream.Read_Socks4_RequestResponse_Async(_cancellationToken);
                    if (socks4_RequestResponse.REP == Socks4_REP.RequestGranted)
                    {
                        return this;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks4ProxySourceTunnel)}.{nameof(InitConnectAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }

            public Stream GetStream()
            {
                return this._stream;
            }
            #endregion


            #region Bind
            Socks4_RequestResponse _socks4_RequestResponse;
            internal async Task<IBindSource> InitBindAsync(Uri address)
            {
                try
                {
                    await ConnectToSocksServer();

                    Socks4_Request socks4_Request = new Socks4_Request(Socks4_CMD.Bind, address, _proxySource.userId);

                    byte[] buffer = socks4_Request.GetByteArray();
                    await this._stream.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);

                    _socks4_RequestResponse = await this._stream.Read_Socks4_RequestResponse_Async(_cancellationToken);
                    if (_socks4_RequestResponse.REP == Socks4_REP.RequestGranted)
                    {
                        return this;
                    }

                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks4ProxySourceTunnel)}.{nameof(InitBindAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }
            public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_socks4_RequestResponse.IPEndPoint);
            }
            public Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_stream);
            }
            #endregion



            async Task ConnectToSocksServer()
            {
                await _tcpClient.ConnectAsync(_proxySource.iPEndPoint.Address, _proxySource.iPEndPoint.Port);
                this._stream = _tcpClient.GetStream();
            }

        }

    }
}
