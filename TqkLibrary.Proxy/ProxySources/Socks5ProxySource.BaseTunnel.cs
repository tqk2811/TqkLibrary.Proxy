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
using System.Net.Http;
using System.Runtime.InteropServices.ComTypes;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class BaseTunnel : BaseProxySourceTunnel<Socks5ProxySource>
        {
            protected const byte SOCKS5_VER = 0x05;
            protected const byte UsernamePassword_Ver = 0x01;

            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream _stream;

            internal BaseTunnel(
                Socks5ProxySource proxySource,
                CancellationToken cancellationToken = default
                )
                : base(
                     proxySource,
                     cancellationToken
                     )
            {

            }

            protected override void Dispose(bool isDisposing)
            {
                _stream?.Dispose();
                _tcpClient.Dispose();
                base.Dispose(isDisposing);
            }



            /// <summary>
            /// Exception on failed
            /// </summary>
            /// <returns></returns>
            /// <exception cref="InvalidDataException"></exception>
            /// <exception cref="InvalidOperationException"></exception>
            /// <exception cref="NotSupportedException"></exception>
            /// <exception cref="Exception"></exception>
            protected async Task InitAsync()
            {
                await _InitConnectionAsync();
                Socks5_Auth socks5_Auth = await _ClientGreetingAsync(_GetSupportAuth());
                await _AuthAsync(socks5_Auth);
            }

            async Task _InitConnectionAsync()
            {
                await _tcpClient.ConnectAsync(_proxySource.IPEndPoint.Address, _proxySource.IPEndPoint.Port);
                _stream = _tcpClient.GetStream();
            }

            IEnumerable<Socks5_Auth> _GetSupportAuth()
            {
                if (_proxySource.HttpProxyAuthentication != null) yield return Socks5_Auth.UsernamePassword;
                yield return Socks5_Auth.NoAuthentication;
            }

            Task<Socks5_Auth> _ClientGreetingAsync(IEnumerable<Socks5_Auth> auths)
                => _ClientGreetingAsync(auths.ToArray());
            async Task<Socks5_Auth> _ClientGreetingAsync(params Socks5_Auth[] auths)
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


            async Task _AuthAsync(Socks5_Auth socks5_Auth)
            {
                if (_GetSupportAuth().Contains(socks5_Auth))
                {
                    switch (socks5_Auth)
                    {
                        case Socks5_Auth.NoAuthentication:
                            return;

                        case Socks5_Auth.UsernamePassword:
                            {
                                Socks5_UsernamePassword socks5_UsernamePassword = new Socks5_UsernamePassword(
                                    _proxySource.HttpProxyAuthentication.UserName,
                                    _proxySource.HttpProxyAuthentication.Password);
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
