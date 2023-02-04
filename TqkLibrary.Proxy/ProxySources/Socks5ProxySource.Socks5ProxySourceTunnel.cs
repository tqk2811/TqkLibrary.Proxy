using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class Socks5ProxySourceTunnel : BaseProxySourceTunnel<Socks5ProxySource>, IConnectSource, IBindSource, IUdpAssociateSource
        {
            const byte SOCKS5_VER = 0x05;
            internal Socks5ProxySourceTunnel(
                Socks5ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }

            #region Connect
            public async Task<IConnectSource> InitConnectAsync(Uri address)
            {
                try
                {
                    await InitAsync();
                    if (await ConnectionRequestAsync(address))
                    {
                        return this;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks5ProxySourceTunnel)}.{nameof(InitConnectAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }

            async Task<bool> ConnectionRequestAsync(Uri address)
            {
                Socks5_Request socks5_Connection = new Socks5_Request(Socks5_CMD.EstablishStreamConnection, address);
                await _stream.WriteAsync(socks5_Connection.GetByteArray(), _cancellationToken);
                await _stream.FlushAsync(_cancellationToken);

                Socks5_RequestResponse socks5_RequestResponse = await _stream.Read_Socks5_RequestResponse_Async(_cancellationToken);
                if (socks5_RequestResponse.STATUS == Socks5_STATUS.RequestGranted)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public Stream GetStream()
            {
                return this._stream;
            }
            #endregion


            #region Bind
            public async Task<IBindSource> InitBindAsync(Uri address)
            {
                try
                {
                    await InitAsync();
                    if (await BindRequestAsync())
                    {
                        return this;
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks5ProxySourceTunnel)}.{nameof(InitBindAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }

            Socks5_RequestResponse _socks5_RequestResponse = null;
            async Task<bool> BindRequestAsync()
            {
                if (_proxySource.IsSupportBind)
                {
                    Socks5_Request socks5_Connection = new Socks5_Request(Socks5_CMD.EstablishPortBinding, new Uri("http://0.0.0.0:0"));
                    await _stream.WriteAsync(socks5_Connection.GetByteArray(), _cancellationToken);
                    await _stream.FlushAsync(_cancellationToken);

                    _socks5_RequestResponse = await _stream.Read_Socks5_RequestResponse_Async(_cancellationToken);
                    if (_socks5_RequestResponse.STATUS == Socks5_STATUS.RequestGranted)
                    {
                        return true;
                    }
                }

                return false;
            }

            public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_socks5_RequestResponse.IPEndPoint);
            }

            public Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_stream);
            }
            #endregion


            #region Udp
            public Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address)
            {
                try
                {

                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks5ProxySourceTunnel)}.{nameof(InitUdpAssociateAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
                this.Dispose();
                return null;
            }



            #endregion


            async Task InitAsync()
            {
                await InitConnectionAsync();
                Socks5_Auth socks5_Auth = await ClientGreetingAsync(GetSupportAuth());
                await AuthAsync(socks5_Auth);
            }

            async Task InitConnectionAsync()
            {
                await _tcpClient.ConnectAsync(_proxySource.IPEndPoint.Address, _proxySource.IPEndPoint.Port);
                _stream = _tcpClient.GetStream();
            }

            IEnumerable<Socks5_Auth> GetSupportAuth()
            {
                if (_proxySource.NetworkCredential != null) yield return Socks5_Auth.UsernamePassword;
                yield return Socks5_Auth.NoAuthentication;
            }

            Task<Socks5_Auth> ClientGreetingAsync(IEnumerable<Socks5_Auth> auths)
                => ClientGreetingAsync(auths.ToArray());

            /// <summary>
            /// return server choise
            /// </summary>
            async Task<Socks5_Auth> ClientGreetingAsync(params Socks5_Auth[] auths)
            {
                if (auths == null || auths.Length == 0)
                    throw new InvalidDataException($"{nameof(auths)} is null or empty array");

                Socks5_Greeting socks5_Greeting = new Socks5_Greeting(auths);
                await _stream.WriteAsync(socks5_Greeting.GetByteArray());
                await _stream.FlushAsync();

                Socks5_GreetingResponse socks5_GreetingResponse = await _stream.Read_Socks5_GreetingResponse_Async(_cancellationToken);
                if (socks5_GreetingResponse.VER != SOCKS5_VER)
                    throw new InvalidOperationException($"Server not support socks5");

                return socks5_GreetingResponse.CAUTH;
            }


            const byte UsernamePassword_Ver = 0x01;
            async Task AuthAsync(Socks5_Auth socks5_Auth)
            {
                if (GetSupportAuth().Contains(socks5_Auth))
                {
                    switch (socks5_Auth)
                    {
                        case Socks5_Auth.NoAuthentication:
                            return;

                        case Socks5_Auth.UsernamePassword:
                            {
                                Socks5_UsernamePassword socks5_UsernamePassword = new Socks5_UsernamePassword(
                                    _proxySource.NetworkCredential.UserName,
                                    _proxySource.NetworkCredential.Password);
                                await _stream.WriteAsync(socks5_UsernamePassword.GetByteArray(), _cancellationToken);
                                await _stream.FlushAsync(_cancellationToken);

                                Socks5_UsernamePasswordResponse socks5_UsernamePasswordResponse = await _stream.Read_Socks5_UsernamePasswordResponse_Async(_cancellationToken);
                                if (socks5_UsernamePasswordResponse.STATUS != 0)
                                    throw new Exception($"{nameof(Socks5_Auth)}.{nameof(Socks5_Auth.UsernamePassword)} failed: " +
                                        $"server response 0x{socks5_UsernamePasswordResponse.VER:X2}{socks5_UsernamePasswordResponse.STATUS:X2}");
                            }
                            return;
                    }
                }
                else
                {
                    throw new NotSupportedException($"Not support auth type {socks5_Auth} ({((byte)socks5_Auth):X2})");
                }
            }


        }
    }
}
