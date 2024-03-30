using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class Socks4ProxyServer : BaseLogger, IProxyServer, ISocks4Proxy
    {
        public bool IsAllowSocks4A { get; set; } = true;


        Stream? _clientStream;
        IPEndPoint? _clientEndPoint;
        IProxyServerHandler? _proxyServerHandler;
        CancellationToken _cancellationToken;
        BaseUserInfo? userInfo;

        public async Task ProxyWorkAsync(
            Stream clientStream,
            IPEndPoint clientEndPoint,
            IProxyServerHandler proxyServerHandler,
            CancellationToken cancellationToken = default
            )
        {
            if (_clientStream is not null)
                throw new InvalidOperationException($"Please create new instance of {nameof(Socks4ProxyServer)} per connection");

            _clientStream = clientStream;
            _clientEndPoint = clientEndPoint;
            _proxyServerHandler = proxyServerHandler;
            _cancellationToken = cancellationToken;


            Socks4_Request socks4_Request = await _clientStream.Read_Socks4_Request_Async(_cancellationToken);
            if (socks4_Request.IsDomain && !IsAllowSocks4A)//socks4a
            {
                await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                return;
            }

            userInfo = new BaseUserInfo(clientEndPoint);



            if (!await proxyServerHandler.IsAcceptUserAsync(userInfo, cancellationToken))
            {
                await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                return;
            }

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
            Uri uri;
            if (socks4_Request.IsDomain)
            {
                if (string.IsNullOrWhiteSpace(socks4_Request.DOMAIN))
                {
                    await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                    return;
                }

                uri = new Uri($"tcp://{socks4_Request.DOMAIN}:{socks4_Request.DSTPORT}");
                if (await _proxyServerHandler!.IsAcceptDomainAsync(uri, userInfo!, _cancellationToken))
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
                uri = new Uri($"tcp://{socks4_Request.DSTIP}:{socks4_Request.DSTPORT}");
                if (await _proxyServerHandler!.IsAcceptDomainAsync(uri, userInfo!, _cancellationToken))
                {
                    target_ip = socks4_Request.DSTIP;
                }
                else
                {
                    await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                    return;
                }
            }

            IProxySource proxySource = await _proxyServerHandler!.GetProxySourceAsync(uri, userInfo!, _cancellationToken);

            Uri uri_connect = new Uri($"http://{target_ip}:{socks4_Request.DSTPORT}");
            using IConnectSource connectSource = proxySource.GetConnectSource();
            await connectSource.ConnectAsync(uri_connect, _cancellationToken);

            using Stream session_stream = await connectSource.GetStreamAsync();

            //send response to client
            await _WriteReplyAsync(Socks4_REP.RequestGranted);

            using Stream clientStream = await _proxyServerHandler.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);
            //transfer until disconnect
            await new StreamTransferHelper(clientStream, session_stream)
                .DebugName(_clientEndPoint, uri_connect)
                .WaitUntilDisconnect(_cancellationToken);
        }

        async Task _HandleBindAsync()
        {
            IProxySource proxySource = await _proxyServerHandler!.GetProxySourceAsync(null, userInfo!, _cancellationToken);
            if (!proxySource.IsSupportBind)
            {
                await _WriteReplyAsync(Socks4_REP.RequestRejectedOrFailed);
                return;
            }

            using IBindSource bindSource = proxySource.GetBindSource();
            IPEndPoint iPEndPoint = await bindSource.BindAsync(_cancellationToken);

            await _WriteReplyAsync(Socks4_REP.RequestGranted, iPEndPoint.Address, (UInt16)iPEndPoint.Port);
            using Stream stream = await bindSource.GetStreamAsync(_cancellationToken);

            using Stream clientStream = await _proxyServerHandler.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);
            //transfer until disconnect
            await new StreamTransferHelper(clientStream, stream)
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

            await _clientStream!.WriteAsync(rep_buffer, _cancellationToken);
            await _clientStream!.FlushAsync(_cancellationToken);
        }
    }
}
