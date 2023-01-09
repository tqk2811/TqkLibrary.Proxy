using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
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
                /*  Socks4
             *              VER	    CMD	    DSTPORT     DSTIP   ID
             *  Byte Count	1	    1	    2	        4	    Variable
             *  -----------------------------------------------------
             *  Socks4a
             *              VER	    CMD	    DSTPORT     DSTIP   ID          DOMAIN
             *  Byte Count	1	    1	    2	        4	    Variable    variable
             *  
             *  in Socks4a, DSTIP willbe 0.0.0.x (x != 0)
             *  -----------------------------------------------------
             *  variable is string null (0x00) terminated
             */

                byte[] data_buffer = await _clientStream.ReadBytesAsync(8, _cancellationToken);
                byte[] id = await _clientStream.ReadUntilNullTerminated(cancellationToken: _cancellationToken);
                byte[] host = null;
                bool isSocks4A = false;
                if (data_buffer[4] == 0 && data_buffer[5] == 0 && data_buffer[6] == 0 && data_buffer[7] != 0)//socks4a
                {
                    isSocks4A = true;
                    if (_proxyServer.IsUseSocks4A)
                    {
                        host = await _clientStream.ReadUntilNullTerminated(cancellationToken: _cancellationToken);
                    }
                    else return;//disconnect
                }
                if (isSocks4A && host == null)//when IsUseSocks4A is false
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

                Socks4_CMD cmd = (Socks4_CMD)data_buffer[1];
                UInt16 port = BitConverter.ToUInt16(data_buffer, 2);
                IPAddress target_ip = null;
                if (host == null)
                {
                    target_ip = new IPAddress(data_buffer.Skip(4).Take(4).ToArray());
                }
                else
                {
                    string domain = Encoding.ASCII.GetString(host);
                    if (string.IsNullOrWhiteSpace(domain))
                    {
                        await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }

                    //ipv4 only because need to response
                    target_ip = Dns.GetHostAddresses(domain).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                    if (target_ip == null)
                    {
                        await WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }
                }

                //release memory
                data_buffer = null;
                id = null;
                host = null;

                //connect to target
                switch (cmd)
                {
                    case Socks4_CMD.EstablishStreamConnection:
                        await EstablishStreamConnectionAsync(target_ip, port);
                        return;

                    case Socks4_CMD.EstablishPortBinding:
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
                IConnectionSource connectionSource = null;
                Stream session_stream = null;
                try
                {
                    try
                    {
                        connectionSource = await _proxyServer.ProxySource.InitConnectionAsync(uri, _cancellationToken);
                        session_stream = connectionSource.GetStream();
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
                    connectionSource?.Dispose();
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
