using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.StreamHeplers;
using TqkLibrary.Proxy.Interfaces;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Helpers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks5ProxyServer
    {
        class Socks5ProxyServerTunnel : BaseProxyServerTunnel<Socks5ProxyServer>
        {
            const byte SOCKS5_VER = 0x05;
            internal Socks5ProxyServerTunnel(
                Socks5ProxyServer proxyServer,
                Stream clientStream,
                EndPoint clientEndPoint,
                CancellationToken _cancellationToken = default
                )
                : base(
                      proxyServer,
                      clientStream,
                      clientEndPoint,
                      _cancellationToken
                      )
            {
            }


            internal override async Task ProxyWorkAsync()
            {
                if (await _ClientGreeting_And_ServerChoice())
                {
                    await _ClientConnectionRequest();
                }
            }

            async Task<bool> _ClientGreeting_And_ServerChoice()
            {
                /*
                 * 	                VER	    NAUTH	AUTH
                 * 	Byte count	    1	    1	    variable
                 */

                //-------------------Client greeting-------------------//
                Socks5_Greeting socks5_Greeting = await _clientStream.Read_Socks5_Greeting_Async(_cancellationToken);
                //-------------------Server choice-------------------//
                Socks5_Auth choice = await _proxyServer.Handler.ChoseAuthAsync(socks5_Greeting.Auths, _cancellationToken);

                Socks5_GreetingResponse greetingResponse = new Socks5_GreetingResponse(choice);
                await _clientStream.WriteAsync(greetingResponse.GetByteArray(), _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);

                return choice != Socks5_Auth.Reject;
            }

            async Task _ClientConnectionRequest()
            {
                Socks5_Request socks5_Request = await _clientStream.Read_Socks5_Request_Async(_cancellationToken);
                if (await _proxyServer.Handler.IsAcceptDomainFilterAsync(socks5_Request.Uri, _cancellationToken))
                {
                    switch(socks5_Request.CMD)
                    {
                        case Socks5_CMD.EstablishStreamConnection:
                            await _EstablishStreamConnectionAsync(socks5_Request.Uri);
                            break;

                        case Socks5_CMD.EstablishPortBinding:
                            await _EstablishPortBinding();
                            break;

                        case Socks5_CMD.AssociateUDP:
                        default:
                            throw new NotSupportedException($"{nameof(Socks5_CMD)}: {socks5_Request.CMD:X2}");
                    }
                }
                else
                {
                    await _WriteReplyConnectionRequestAsync(Socks5_STATUS.ConnectionNotAllowedByRuleset);
                }
            }

            Task _WriteReplyConnectionRequestAsync(Socks5_STATUS status)
                => _WriteReplyConnectionRequestAsync(status, new IPEndPoint(IPAddress.Any, 0));

            async Task _WriteReplyConnectionRequestAsync(
                Socks5_STATUS status,
                IPEndPoint iPEndPoint
                )
            {
                Socks5_RequestResponse socks5_RequestResponse = new Socks5_RequestResponse(status, iPEndPoint);
                byte[] rep_buffer = socks5_RequestResponse.GetByteArray();

                _logger?.LogInformation($"{_clientEndPoint} <- 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");

                await _clientStream.WriteAsync(rep_buffer, _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
            }

            async Task _EstablishStreamConnectionAsync(Uri uri)
            {
                IProxySource proxySource = await _proxyServer.Handler.GetProxySourceAsync(_cancellationToken);
                using IConnectSource connectSource = proxySource.GetConnectSource();
                await connectSource.ConnectAsync(uri, _cancellationToken);
                using Stream session_stream = await connectSource.GetStreamAsync();
                //send response to client
                await _WriteReplyConnectionRequestAsync(Socks5_STATUS.RequestGranted);

                //transfer until disconnect
                await new StreamTransferHelper(_clientStream, session_stream)
                    .DebugName(_clientEndPoint, uri)
                    .WaitUntilDisconnect(_cancellationToken);
            }

            async Task _EstablishPortBinding()
            {
                IProxySource proxySource = await _proxyServer.Handler.GetProxySourceAsync(_cancellationToken);
                using IBindSource bindSource = proxySource.GetBindSource();
                IPEndPoint listen_endpoint = await bindSource.BindAsync(_cancellationToken);

                await _WriteReplyConnectionRequestAsync(Socks5_STATUS.RequestGranted, listen_endpoint);

                Stream target_stream = await bindSource.GetStreamAsync(_cancellationToken);
                //transfer until disconnect
                await new StreamTransferHelper(_clientStream, target_stream)
                    .DebugName(_clientEndPoint, listen_endpoint)
                    .WaitUntilDisconnect(_cancellationToken);
            }


        }
    }
}
