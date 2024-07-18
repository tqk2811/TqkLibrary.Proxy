using System.Net.Sockets;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        class BaseTunnel : BaseProxySourceTunnel<Socks5ProxySource>
        {
            protected const byte SOCKS5_VER = 0x05;
            protected const byte UsernamePassword_Ver = 0x01;

            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream? _stream;

            internal BaseTunnel(Socks5ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
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
            protected async Task ConnectAndAuthAsync(CancellationToken cancellationToken = default)
            {
#if NET5_0_OR_GREATER
                await _tcpClient.ConnectAsync(_proxySource.IPEndPoint.Address, _proxySource.IPEndPoint.Port, cancellationToken);
#else
                await _tcpClient.ConnectAsync(_proxySource.IPEndPoint.Address, _proxySource.IPEndPoint.Port);
#endif
                _stream = _tcpClient.GetStream();



                Socks5_Auth socks5_Auth = await _ClientGreetingAsync(_GetSupportAuth(), cancellationToken);
                await _AuthAsync(socks5_Auth, cancellationToken);


            }

            IEnumerable<Socks5_Auth> _GetSupportAuth()
            {
                if (_proxySource.HttpProxyAuthentication != null) yield return Socks5_Auth.UsernamePassword;
                yield return Socks5_Auth.NoAuthentication;
            }

            async Task<Socks5_Auth> _ClientGreetingAsync(IEnumerable<Socks5_Auth> auths, CancellationToken cancellationToken = default)
            {
                if (auths == null || !auths.Any())
                    throw new InvalidDataException($"{nameof(auths)} is null or empty");
                if (_stream is null)
                    throw new InvalidOperationException();

                Socks5_Greeting socks5_Greeting = new Socks5_Greeting(auths);
                await _stream.WriteAsync(socks5_Greeting.GetByteArray());
                await _stream.FlushAsync();

                Socks5_GreetingResponse socks5_GreetingResponse = await _stream.Read_Socks5_GreetingResponse_Async(cancellationToken);
                if (socks5_GreetingResponse.VER != SOCKS5_VER)
                    throw new InvalidOperationException($"Server not support socks5");

                return socks5_GreetingResponse.CAUTH;
            }


            async Task _AuthAsync(Socks5_Auth socks5_Auth, CancellationToken cancellationToken = default)
            {
                if (_stream is null) throw new InvalidOperationException();

                if (_GetSupportAuth().Contains(socks5_Auth))
                {
                    switch (socks5_Auth)
                    {
                        case Socks5_Auth.NoAuthentication:
                            return;

                        case Socks5_Auth.UsernamePassword:
                            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                Socks5_UsernamePassword socks5_UsernamePassword = new Socks5_UsernamePassword(
                                    _proxySource.HttpProxyAuthentication.UserName,
                                    _proxySource.HttpProxyAuthentication.Password);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                await _stream.WriteAsync(socks5_UsernamePassword.GetByteArray(), cancellationToken);
                                await _stream.FlushAsync(cancellationToken);

                                Socks5_UsernamePasswordResponse socks5_UsernamePasswordResponse
                                    = await _stream.Read_Socks5_UsernamePasswordResponse_Async(cancellationToken);

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
