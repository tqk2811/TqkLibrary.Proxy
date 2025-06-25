using Microsoft.Extensions.Logging;
using System.Net;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class Socks5ProxyServer : BaseLogger, IProxyServer, ISocks5Proxy
    {
        class Socks5UserInfo : BaseUserInfo
        {
            public Socks5UserInfo(IPEndPoint iPEndPoint, Guid tunnelId) : base(iPEndPoint, tunnelId)
            {

            }

            public Socks5Authentication? Socks5Authentication { get; set; }
            public override IAuthentication? Authentication
            {
                get => Socks5Authentication;
                set => throw new NotImplementedException();
            }
        }

        Stream? _clientStream;
        IPEndPoint? _clientEndPoint;
        IProxyServerHandler? _proxyServerHandler;
        Guid _tunnelId;
        CancellationToken _cancellationToken;
        Socks5UserInfo? userInfo;

        public async Task ProxyWorkAsync(
            Stream clientStream,
            IPEndPoint clientEndPoint,
            IProxyServerHandler proxyServerHandler,
            Guid tunnelId,
            CancellationToken cancellationToken = default
            )
        {
            if (_clientStream is not null)
                throw new InvalidOperationException($"Please create new instance of {nameof(Socks5ProxyServer)} per connection");

            _clientStream = clientStream;
            _clientEndPoint = clientEndPoint;
            _proxyServerHandler = proxyServerHandler;
            _tunnelId = tunnelId;
            _cancellationToken = cancellationToken;

            userInfo = new Socks5UserInfo(clientEndPoint, _tunnelId);

            if (await _ClientGreeting_And_ServerChoiceAsync())
            {
                await _ClientConnectionRequestAsync();
            }
        }

        async Task<bool> _ClientGreeting_And_ServerChoiceAsync()
        {
            /*
             * 	                VER	    NAUTH	AUTH
             * 	Byte count	    1	    1	    variable
             */

            //-------------------Client greeting-------------------//
            Socks5_Greeting socks5_Greeting = await _clientStream!.Read_Socks5_Greeting_Async(_cancellationToken);
            userInfo!.Socks5Authentication = new Socks5Authentication(socks5_Greeting.Auths);
            //-------------------Server choice-------------------//
            Socks5_Auth choice = Socks5_Auth.Reject;
            if (await _proxyServerHandler!.IsAcceptUserAsync(userInfo, _cancellationToken))
            {
                choice = userInfo!.Socks5Authentication.Choice;
            }
            Socks5_GreetingResponse greetingResponse = new Socks5_GreetingResponse(choice);
            await _clientStream!.WriteAsync(greetingResponse.GetByteArray(), _cancellationToken);
            await _clientStream!.FlushAsync(_cancellationToken);
            return choice != Socks5_Auth.Reject;
        }

        async Task _ClientConnectionRequestAsync()
        {
            Socks5_Request socks5_Request = await _clientStream!.Read_Socks5_Request_Async(_cancellationToken);
            if (await _proxyServerHandler!.IsAcceptDomainAsync(socks5_Request.Uri, userInfo!, _cancellationToken))
            {
                switch (socks5_Request.CMD)
                {
                    case Socks5_CMD.EstablishStreamConnection:
                        await _EstablishStreamConnectionAsync(socks5_Request.Uri);
                        break;

                    case Socks5_CMD.EstablishPortBinding:
                        await _EstablishPortBinding();
                        break;

                    case Socks5_CMD.AssociateUDP:


                    default:
                        await _WriteReplyConnectionRequestAsync(Socks5_STATUS.CommandNotSupportedOrProtocolError);
                        break;
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

            _logger?.LogInformation($"{_tunnelId} {_clientEndPoint} <- 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");

            await _clientStream!.WriteAsync(rep_buffer, _cancellationToken);
            await _clientStream!.FlushAsync(_cancellationToken);
        }

        async Task _EstablishStreamConnectionAsync(Uri uri)
        {
            IProxySource proxySource = await _proxyServerHandler!.GetProxySourceAsync(uri, userInfo!, _cancellationToken);
            using IConnectSource connectSource = await proxySource.GetConnectSourceAsync(_tunnelId);
            await connectSource.ConnectAsync(uri, _cancellationToken);
            using Stream session_stream = await connectSource.GetStreamAsync();
            //send response to client
            await _WriteReplyConnectionRequestAsync(Socks5_STATUS.RequestGranted);

            using Stream clientStream = await _proxyServerHandler.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);

            await new StreamTransferHelper(clientStream, session_stream, _tunnelId)
                .DebugName(_clientEndPoint, uri)
                .WaitUntilDisconnect(_cancellationToken);
        }

        async Task _EstablishPortBinding()
        {
            IProxySource proxySource = await _proxyServerHandler!.GetProxySourceAsync(null, userInfo!, _cancellationToken);
            if (!proxySource.IsSupportBind)
            {
                await _WriteReplyConnectionRequestAsync(Socks5_STATUS.GeneralFailure);
                return;
            }

            using IBindSource bindSource = await proxySource.GetBindSourceAsync(_tunnelId);
            IPEndPoint listen_endpoint = await bindSource.BindAsync(_cancellationToken);

            await _WriteReplyConnectionRequestAsync(Socks5_STATUS.RequestGranted, listen_endpoint);

            Stream target_stream = await bindSource.GetStreamAsync(_cancellationToken);
            using Stream clientStream = await _proxyServerHandler.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);

            await new StreamTransferHelper(clientStream, target_stream, _tunnelId)
                .DebugName(_clientEndPoint, listen_endpoint)
                .WaitUntilDisconnect(_cancellationToken);
        }
    }
}
