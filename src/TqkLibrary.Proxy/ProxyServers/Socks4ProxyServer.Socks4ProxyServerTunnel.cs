using Microsoft.Extensions.Logging;
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
                    !await _proxyServer.Handler.IsUseSocks4AAsync(_cancellationToken))//socks4a
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


                //connect to target
                switch (socks4_Request.CMD)
                {
                    case Socks4_CMD.Connect:
                        await _HandleConnectAsync(socks4_Request);
                        return;

                    case Socks4_CMD.Bind:
                        await _HandleBindAsync();
                        return;

                }
            }

            async Task _HandleConnectAsync(Socks4_Request socks4_Request)
            {
                IPAddress? target_ip = null;
                if (socks4_Request.IsDomain)
                {
                    if (string.IsNullOrWhiteSpace(socks4_Request.DOMAIN))
                    {
                        await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }

                    Uri uri = new Uri($"tcp://{socks4_Request.DOMAIN}:{socks4_Request.DSTPORT}");
                    if (await _proxyServer.Handler.IsAcceptDomainAsync(uri, _cancellationToken))
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
                    if (await _proxyServer.Handler.IsAcceptDomainAsync(uri, _cancellationToken))
                    {
                        target_ip = socks4_Request.DSTIP;
                    }
                    else
                    {
                        await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                        return;
                    }
                }

                IProxySource proxySource = await _proxyServer.Handler.GetProxySourceAsync(_cancellationToken);

                Uri uri_connect = new Uri($"http://{target_ip}:{socks4_Request.DSTPORT}");
                using IConnectSource connectSource = proxySource.GetConnectSource();
                await connectSource.ConnectAsync(uri_connect, _cancellationToken);

                using Stream session_stream = await connectSource.GetStreamAsync();

                //send response to client
                await _WriteReplyAsync(Socks4_REP.RequestGranted);

                //transfer until disconnect
                await new StreamTransferHelper(_clientStream, session_stream)
                    .DebugName(_clientEndPoint, uri_connect)
                    .WaitUntilDisconnect(_cancellationToken);
            }


            async Task _HandleBindAsync()
            {
                IProxySource proxySource = await _proxyServer.Handler.GetProxySourceAsync(_cancellationToken);
                if (!proxySource.IsSupportBind)
                {
                    await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                    return;
                }

                using IBindSource bindSource = proxySource.GetBindSource();
                IPEndPoint iPEndPoint = await bindSource.BindAsync(_cancellationToken);

                await _WriteReplyAsync(Socks4_REP.RequestGranted, iPEndPoint.Address, (UInt16)iPEndPoint.Port);
                using Stream stream = await bindSource.GetStreamAsync(_cancellationToken);

                //transfer until disconnect
                await new StreamTransferHelper(_clientStream, stream)
                    .DebugName(_clientEndPoint, iPEndPoint)
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

                Socks4_RequestResponse response = new Socks4_RequestResponse(rep, listen_ip, listen_port);
                byte[] rep_buffer = response.GetByteArray();

                _logger?.LogInformation($"{_clientEndPoint} <- 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");

                await _clientStream.WriteAsync(rep_buffer, _cancellationToken);
                await _clientStream.FlushAsync(_cancellationToken);
            }
        }
    }
}
