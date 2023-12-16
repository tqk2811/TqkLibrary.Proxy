using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks4ProxyServer
    {
        class Socks4ProxyServerTunnel : BaseProxyServerTunnel<Socks4ProxyServer>
        {
            internal Socks4ProxyServerTunnel(
                Socks4ProxyServer proxyServer,
                Stream clientStream,
                EndPoint clientEndPoint,
                CancellationToken cancellationToken = default
                )
                : base(
                      proxyServer,
                      clientStream,
                      clientEndPoint,
                      cancellationToken
                      )
            {
            }

            internal override async Task ProxyWorkAsync()
            {
                Socks4_Request socks4_Request = await _clientStream.Read_Socks4_Request_Async(_cancellationToken);
                if (socks4_Request.IsDomain &&
                    !await _proxyServer.Filter.IsUseSocks4AAsync(_cancellationToken))//socks4a
                {
                    await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                    return;
                }

                //check auth id
                //if(failed)
                //{
                //    await WriteReplyAsync(stream, Socks4_REP.CouldNotConfirmTheUserId, remoteEndPoint);
                //    return;
                //}

                IPAddress? target_ip = null;
                if (socks4_Request.IsDomain)
                {
                    if (string.IsNullOrWhiteSpace(socks4_Request.DOMAIN))
                    {
                        await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }

                    Uri uri = new Uri($"tcp://{socks4_Request.DOMAIN}:{socks4_Request.DSTPORT}");
                    if (await _proxyServer.Filter.IsAcceptDomainFilterAsync(uri, _cancellationToken))
                    {
                        //ipv4 only because need to response
                        target_ip = Dns.GetHostAddresses(socks4_Request.DOMAIN).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                        if (target_ip is null)
                        {
                            await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                            return;
                        }
                    }
                    else
                    {
                        await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }
                }
                else
                {
                    Uri uri = new Uri($"tcp://{socks4_Request.DSTIP}:{socks4_Request.DSTPORT}");
                    if (await _proxyServer.Filter.IsAcceptDomainFilterAsync(uri, _cancellationToken))
                    {
                        target_ip = socks4_Request.DSTIP;
                    }
                    else
                    {
                        await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }
                }

                //connect to target
                switch (socks4_Request.CMD)
                {
                    case Socks4_CMD.Connect:
                        await _EstablishStreamConnectionAsync(target_ip, socks4_Request.DSTPORT);
                        return;

                    case Socks4_CMD.Bind:
                        if (_proxyServer.ProxySource.IsSupportBind)
                        {
                            //not support now, write later
                            //it create listen port on this IProxySource and transfer with current connection
                            //and send reply ip:port listen
                            await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        }
                        else
                        {
                            //not support
                            await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        }
                        return;

                }
            }

            async Task _EstablishStreamConnectionAsync(
                IPAddress target_ip,
                UInt16 target_port
                )
            {
                Uri uri = new Uri($"http://{target_ip}:{target_port}");
                using IConnectSource connectSource = _proxyServer.ProxySource.GetConnectSource();
                await connectSource.InitAsync(uri, _cancellationToken);

                using Stream session_stream = await connectSource.GetStreamAsync();

                //send response to client
                await _WriteReplyAsync(Socks4_REP.RequestGranted);

                //transfer until disconnect
                await new StreamTransferHelper(_clientStream, session_stream)
#if DEBUG
                    .DebugName(_clientEndPoint.ToString(), uri.ToString())
#endif
                    .WaitUntilDisconnect(_cancellationToken);
            }

            Task _WriteReplyAsync(Socks4_REP rep) => _WriteReplyAsync(rep, IPAddress.Any, 0);

            async Task _WriteReplyAsync(
                Socks4_REP rep,
                IPAddress listen_ip,
                UInt16 listen_port)
            {
                if (listen_ip.AddressFamily != AddressFamily.InterNetwork)
                    throw new InvalidDataException($"{nameof(listen_ip)}.{nameof(AddressFamily)} must be {nameof(AddressFamily.InterNetwork)}");

                byte[] rep_buffer = new byte[8];
                rep_buffer[0] = 0;
                rep_buffer[1] = (byte)rep;
                rep_buffer[2] = (byte)(listen_port >> 8);
                rep_buffer[3] = (byte)listen_port;
                listen_ip.GetAddressBytes().CopyTo(rep_buffer, 4);
#if DEBUG
                Console.WriteLine($"[{nameof(Socks4ProxyServerTunnel)}.{nameof(_WriteReplyAsync)}] {_clientEndPoint} << 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");
#endif
                await _clientStream.WriteAsync(rep_buffer, _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
            }
        }
    }
}
