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
                if (socks4_Request.IsDomain && !_proxyServer.IsUseSocks4A)//socks4a
                {
                    await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                    return;
                }

                //check auth id
                //if(failed)
                //{
                //    await WriteReplyAsync(stream, Socks4_REP.CouldNotConfirmTheUserId, remoteEndPoint);
                //    return;
                //}

                IPAddress target_ip = null;
                if (socks4_Request.IsDomain)
                {
                    if (string.IsNullOrWhiteSpace(socks4_Request.DOMAIN))
                    {
                        await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }

                    //ipv4 only because need to response
                    target_ip = Dns.GetHostAddresses(socks4_Request.DOMAIN).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                    if (target_ip == null)
                    {
                        await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }
                }
                else
                {
                    target_ip = socks4_Request.DSTIP;
                }

                //connect to target
                switch (socks4_Request.CMD)
                {
                    case Socks4_CMD.Connect:
                        await EstablishStreamConnectionAsync(target_ip, socks4_Request.DSTPORT);
                        return;

                    case Socks4_CMD.Bind:
                        if (_proxyServer.ProxySource.IsSupportBind)
                        {
                            //not support now, write later
                            //it create listen port on this IProxySource and transfer with current connection
                            //and send reply ip:port listen
                            await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        }
                        else
                        {
                            //not support
                            await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        }
                        return;

                }
            }




            async Task EstablishStreamConnectionAsync(
                IPAddress target_ip,
                UInt16 target_port
                )
            {
                Uri uri = new Uri($"http://{target_ip}:{target_port}");
                IConnectSource connectSource = null;
                Stream session_stream = null;
                try
                {
                    try
                    {
                        connectSource = await _proxyServer.ProxySource.InitConnectAsync(uri, _cancellationToken);
                        session_stream = connectSource.GetStream();
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine($"[{nameof(Socks4ProxyServerTunnel)}.{nameof(EstablishStreamConnectionAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                        await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }

                    //send response to client
                    await WriteReplyAsync(Socks4_REP.RequestGranted);

                    //transfer until disconnect
                    await new StreamTransferHelper(_clientStream, session_stream)
#if DEBUG
                        .DebugName(_clientEndPoint.ToString(), uri.ToString())
#endif
                        .WaitUntilDisconnect(_cancellationToken);
                }
                finally
                {
                    session_stream?.Dispose();
                    connectSource?.Dispose();
                }
            }



            Task WriteReplyAsync(Socks4_REP rep) => WriteReplyAsync(rep, IPAddress.Any, 0);

            async Task WriteReplyAsync(
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
                Console.WriteLine($"[{nameof(Socks4ProxyServerTunnel)}.{nameof(WriteReplyAsync)}] {_clientEndPoint} << 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");
#endif
                await _clientStream.WriteAsync(rep_buffer, _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
            }
        }
    }
}
