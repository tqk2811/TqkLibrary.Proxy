using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        public class BaseTunnel : BaseProxySourceTunnel<Socks5ProxySource>
        {
            protected const byte SOCKS5_VER = 0x05;
            protected const byte UsernamePassword_Ver = 0x01;

            protected readonly ILogger? _logger;

            protected readonly TcpClient _tcpClient = new TcpClient();
            protected Stream? _stream;

            internal protected BaseTunnel(Socks5ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {
                _logger = proxySource._loggerFactory?.CreateLogger(GetType());
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
            protected virtual async Task ConnectAndAuthAsync(CancellationToken cancellationToken = default)
            {
                _logger?.LogInformation("TCP connect -> {UpstreamUri}", _proxySource.Uri);
                try
                {
#if NET5_0_OR_GREATER
                    await _tcpClient.ConnectAsync(_proxySource.Uri.DnsSafeHost, _proxySource.Uri.Port, cancellationToken);
#else
                    await _tcpClient.ConnectAsync(_proxySource.Uri.DnsSafeHost, _proxySource.Uri.Port);
#endif
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "TCP connect FAILED -> {UpstreamUri}", _proxySource.Uri);
                    throw;
                }
                _stream = _tcpClient.GetStream();
                _logger?.LogInformation("TCP connect OK; starting SOCKS5 greeting");

                Socks5_Auth socks5_Auth = await _ClientGreetingAsync(_GetSupportAuth(), cancellationToken);
                _logger?.LogInformation("Greeting OK; server selected auth={Auth} (0x{AuthByte:X2})", socks5_Auth, (byte)socks5_Auth);
                await _AuthAsync(socks5_Auth, cancellationToken);
                _logger?.LogInformation("Auth OK");
            }

            protected virtual IEnumerable<Socks5_Auth> _GetSupportAuth()
            {
                if (_proxySource.Credential != null) yield return Socks5_Auth.UsernamePassword;
                yield return Socks5_Auth.NoAuthentication;
            }

            protected virtual async Task<Socks5_Auth> _ClientGreetingAsync(IEnumerable<Socks5_Auth> auths, CancellationToken cancellationToken = default)
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


            protected virtual async Task _AuthAsync(Socks5_Auth socks5_Auth, CancellationToken cancellationToken = default)
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
                                    _proxySource.Credential.UserName,
                                    _proxySource.Credential.Password);
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
