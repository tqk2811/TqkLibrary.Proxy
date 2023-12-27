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


            #region Client greeting & Server choice

            async Task<bool> _ClientGreeting_And_ServerChoice()
            {
                /*
                 * 	                VER	    NAUTH	AUTH
                 * 	Byte count	    1	    1	    variable
                 */

                //-------------------Client greeting-------------------//
                byte[] data_buffer = await _clientStream.ReadBytesAsync(2, _cancellationToken);
                byte[] auths_buffer = await _clientStream.ReadBytesAsync(data_buffer[1]);
                Socks5_Auth[] auths = auths_buffer.Select(x => (Socks5_Auth)x).ToArray();

                //-------------------Server choice-------------------//
                Socks5_Auth choice = await _proxyServer.Handler.ChoseAuthAsync(auths, _cancellationToken);
                await _ServerChoiceResponseAsync(choice);
                return choice != Socks5_Auth.Reject;
            }

            async Task _ServerChoiceResponseAsync(Socks5_Auth socks5_Auth)
            {
                byte[] buffer = new byte[2]
                {
                    SOCKS5_VER,
                    (byte)socks5_Auth
                };
                await _clientStream.WriteAsync(buffer, _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
            }

            #endregion





            #region Client connection request

            async Task _ClientConnectionRequest()
            {
                byte[] data_buffer = await _clientStream.ReadBytesAsync(3);
                Uri uri = await _Read_DSTADDR_DSTPORT_Async();
                if (await _proxyServer.Handler.IsAcceptDomainFilterAsync(uri, _cancellationToken))
                {
                    switch ((Socks5_CMD)data_buffer[1])
                    {
                        case Socks5_CMD.EstablishStreamConnection:
                            await _EstablishStreamConnectionAsync(uri);
                            break;

                        case Socks5_CMD.EstablishPortBinding:
                            await _EstablishPortBinding();
                            break;

                        case Socks5_CMD.AssociateUDP:
                        default:
                            throw new NotSupportedException($"{nameof(Socks5_CMD)}: {data_buffer[1]:X2}");
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            async Task<Uri> _Read_DSTADDR_DSTPORT_Async()
            {
                byte[] buffer = await _clientStream.ReadBytesAsync(1);

                IPAddress? ipAddress = null;
                string domain = string.Empty;
                switch ((Socks5_ATYP)buffer[0])
                {
                    case Socks5_ATYP.IpV4:
                    case Socks5_ATYP.IpV6:
                        buffer = await _clientStream.ReadBytesAsync((Socks5_ATYP)buffer[0] == Socks5_ATYP.IpV4 ? 4 : 16);
                        ipAddress = new IPAddress(buffer);
                        break;

                    case Socks5_ATYP.DomainName:
                        //read domain length
                        buffer = await _clientStream.ReadBytesAsync(1);
                        //read domain
                        buffer = await _clientStream.ReadBytesAsync(buffer[0]);
                        domain = Encoding.ASCII.GetString(buffer);
                        break;

                    default:
                        throw new NotSupportedException($"{nameof(Socks5_ATYP)}: {buffer[0]:X2}");
                }
                //read des port
                buffer = await _clientStream.ReadBytesAsync(2);
                UInt16 DSTPORT = BitConverter.ToUInt16(buffer.Reverse().ToArray(), 0);

                if (string.IsNullOrWhiteSpace(domain))
                {
                    return new Uri($"http://{ipAddress}:{DSTPORT}");
                }
                else
                {
                    return new Uri($"http://{domain}:{DSTPORT}");
                }
            }

            Task _WriteReplyConnectionRequestAsync(Socks5_STATUS status)
                => _WriteReplyConnectionRequestAsync(status, IPAddress.Any, 0);

            async Task _WriteReplyConnectionRequestAsync(
                Socks5_STATUS status,
                IPAddress listen_ip,
                UInt16 listen_port
                )
            {
                using MemoryStream memoryStream = new MemoryStream();
                memoryStream.WriteByte(SOCKS5_VER);
                memoryStream.WriteByte((byte)status);
                memoryStream.WriteByte(0);
                _Write_BNDADDR(memoryStream, listen_ip);
                memoryStream.WriteByte((byte)(listen_port >> 8));
                memoryStream.WriteByte((byte)listen_port);
                byte[] rep_buffer = memoryStream.ToArray();

                _logger?.LogInformation($"{_clientEndPoint} <- 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");

                await _clientStream.WriteAsync(rep_buffer, _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
            }

            void _Write_BNDADDR(MemoryStream memoryStream, IPAddress iPAddress)
            {
                if (iPAddress.AddressFamily != AddressFamily.InterNetwork && iPAddress.AddressFamily != AddressFamily.InterNetworkV6)
                    throw new InvalidDataException($"{nameof(iPAddress)} must be ipv4 or ipv6");

                var address_bytes = iPAddress.GetAddressBytes();
                memoryStream.WriteByte((byte)(address_bytes.Length == 4 ? Socks5_ATYP.IpV4 : Socks5_ATYP.IpV6));
                memoryStream.Write(address_bytes);
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


                await _WriteReplyConnectionRequestAsync(
                    Socks5_STATUS.RequestGranted,
                    listen_endpoint.Address,
                    (UInt16)listen_endpoint.Port);

                Stream target_stream = await bindSource.GetStreamAsync(_cancellationToken);
                //transfer until disconnect
                await new StreamTransferHelper(_clientStream, target_stream)
                    .DebugName(_clientEndPoint, listen_endpoint)
                    .WaitUntilDisconnect(_cancellationToken);
            }

            #endregion

        }
    }
}
