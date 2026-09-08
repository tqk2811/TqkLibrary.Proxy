using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Authentications;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class Socks5ProxyServer : IProxyServer, ISocks5Proxy
    {
        class Socks5UserInfo : BaseUserInfo
        {
            public Socks5UserInfo(IPEndPoint iPEndPoint, Guid tunnelId) : base(iPEndPoint, tunnelId)
            {

            }

            public Socks5Authentication? Socks5Authentication { get; set; }
            public ProxyCredential? ProxyCredential { get; set; }
            public override IAuthentication? Authentication
            {
                get => (IAuthentication?)ProxyCredential ?? Socks5Authentication;
                set => throw new NotImplementedException();
            }
        }

        readonly ILoggerFactory? _loggerFactory;
        readonly ILogger? _logger;

        Stream? _clientStream;
        IPEndPoint? _clientEndPoint;
        IProxyServerHandler? _proxyServerHandler;
        Guid _tunnelId;
        CancellationToken _cancellationToken;
        Socks5UserInfo? userInfo;

        public Socks5ProxyServer(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<Socks5ProxyServer>();
        }

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

            if (choice == Socks5_Auth.Reject)
                return false;

            // RFC 1929 sub-negotiation when UsernamePassword is selected.
            if (choice == Socks5_Auth.UsernamePassword)
            {
                Socks5_UsernamePassword credPacket = await _clientStream!.Read_Socks5_UsernamePassword_Async(_cancellationToken);
                userInfo!.ProxyCredential = new ProxyCredential(credPacket.UserName, credPacket.Password);

                bool credOk = await _proxyServerHandler!.IsAcceptUserAsync(userInfo, _cancellationToken);
                byte status = credOk ? (byte)0x00 : (byte)0x01;
                Socks5_UsernamePasswordResponse credResponse = new Socks5_UsernamePasswordResponse(status);
                await _clientStream!.WriteAsync(credResponse.GetByteArray(), _cancellationToken);
                await _clientStream!.FlushAsync(_cancellationToken);
                return credOk;
            }

            return true;
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
                        await _EstablishUdpAssociateAsync(socks5_Request);
                        break;

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

            _logger?.LogInformation("Reply 0x{Reply}", BitConverter.ToString(rep_buffer).Replace("-", ""));

            await _clientStream!.WriteAsync(rep_buffer, _cancellationToken);
            await _clientStream!.FlushAsync(_cancellationToken);
        }

        async Task _EstablishStreamConnectionAsync(Uri uri)
        {
            IProxySource proxySource = await _proxyServerHandler!.GetProxySourceAsync(uri, userInfo!, _cancellationToken);
            using IConnectSource connectSource = await proxySource.GetConnectSourceAsync(_tunnelId);

            Stream session_stream;
            try
            {
                await connectSource.ConnectAsync(uri, _cancellationToken);
                session_stream = await connectSource.GetStreamAsync();
            }
            catch (Exception ex)
            {
                // The protocol has a reply for this, and a client that gets one can say what went
                // wrong. Dropping the connection instead left it waiting on a socket that simply
                // ended — indistinguishable from the proxy itself being broken.
                _logger?.LogInformation(ex, "connecting upstream to {Uri} failed", uri);
                await _WriteReplyConnectionRequestAsync(StatusFor(ex));
                return;
            }

            using (session_stream)
            {
                //send response to client
                await _WriteReplyConnectionRequestAsync(Socks5_STATUS.RequestGranted);

                using Stream clientStream = await _proxyServerHandler.StreamHandlerAsync(_clientStream!, userInfo!, _cancellationToken);

                await new StreamTransferHelper(clientStream, session_stream, _tunnelId, _loggerFactory)
                    .DebugName(_clientEndPoint, uri)
                    .WaitUntilDisconnect(_cancellationToken);
            }
        }

        /// <summary>Which SOCKS5 reply describes this failure to the client.</summary>
        static Socks5_STATUS StatusFor(Exception ex)
        {
            if (ex is SocketException socket)
            {
                switch (socket.SocketErrorCode)
                {
                    case SocketError.ConnectionRefused: return Socks5_STATUS.ConnectionRefusedByDestinationHost;
                    case SocketError.HostUnreachable: return Socks5_STATUS.HostUnreachable;
                    case SocketError.NetworkUnreachable: return Socks5_STATUS.NetworkUnreachable;
                    case SocketError.TimedOut: return Socks5_STATUS.TTL_expired;
                }
            }
            return Socks5_STATUS.GeneralFailure;
        }

        // SOCKS5 UDP ASSOCIATE relay (RFC 1928 §6, §7):
        //   1. Open a server-side UDP socket — this is BND.ADDR:BND.PORT the client sends datagrams to.
        //   2. Get an IUdpAssociateSource as the egress channel (LocalProxySource = local socket;
        //      Socks5ProxySource = chained relay).
        //   3. Run two pumps + a TCP-control watcher in parallel. Tear down everything when the TCP
        //      control connection closes (RFC requirement) or any pump errors out.
        //
        // BND.ADDR strategy: reply with IPAddress.Any (0.0.0.0) / IPv6Any (::) — by widespread
        // convention, clients substitute the TCP control peer IP when they see all-zeros (the same
        // fallback Socks5ProxySource.UdpTunnel does on the client side).
        //
        // Client endpoint locking: the request's DST.ADDR/DST.PORT *may* announce where the client
        // will send from. If 0:0 ("unknown"), accept the first datagram from any port at the client's
        // TCP IP and lock to it. If specific, restrict to that exact endpoint.
        async Task _EstablishUdpAssociateAsync(Socks5_Request request)
        {
            IProxySource proxySource = await _proxyServerHandler!.GetProxySourceAsync(null, userInfo!, _cancellationToken);
            if (!proxySource.IsSupportUdp)
            {
                _logger?.LogWarning("UDP ASSOCIATE rejected: source does not support UDP");
                await _WriteReplyConnectionRequestAsync(Socks5_STATUS.CommandNotSupportedOrProtocolError);
                return;
            }

            using IUdpAssociateSource egress = await proxySource.GetUdpAssociateSourceAsync(_tunnelId);
            try
            {
                await egress.AssociateAsync(_cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "UDP ASSOCIATE egress AssociateAsync failed");
                await _WriteReplyConnectionRequestAsync(Socks5_STATUS.GeneralFailure);
                return;
            }

            // Open the client-facing UDP socket on a family matching the TCP control connection.
            AddressFamily clientFamily = _clientEndPoint!.AddressFamily;
            IPAddress bindAny = clientFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
            using UdpClient clientFacing = new UdpClient(new IPEndPoint(bindAny, 0));
            int udpPort = ((IPEndPoint)clientFacing.Client.LocalEndPoint!).Port;

            // BND.ADDR = Any of the matching family; clients interpret all-zero as "use TCP peer IP".
            IPEndPoint bnd = new IPEndPoint(bindAny, udpPort);
            await _WriteReplyConnectionRequestAsync(Socks5_STATUS.RequestGranted, bnd);
            _logger?.LogInformation("UDP ASSOCIATE granted bnd={Bnd} clientTcp={ClientTcp}", bnd, _clientEndPoint);

            // Strict-match the announced endpoint when client gave one; otherwise lock on first packet.
            IPAddress allowedClientIp = _clientEndPoint!.Address;
            int? requiredClientPort = null;
            if (request.DSTADDR.ATYP != Socks5_ATYP.DomainName)
            {
                IPAddress reqAddr = request.DSTADDR.IPAddress;
                if (!IPAddress.Any.Equals(reqAddr) && !IPAddress.IPv6Any.Equals(reqAddr))
                    allowedClientIp = reqAddr;
            }
            if (request.DSTPORT != 0)
                requiredClientPort = request.DSTPORT;

            using CancellationTokenSource pumpCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken);
            ClientEndpointBox lockedClient = new ClientEndpointBox();

            Task pumpC2T = _UdpClientToTargetPumpAsync(clientFacing, egress, allowedClientIp, requiredClientPort, lockedClient, pumpCts.Token);
            Task pumpT2C = _UdpTargetToClientPumpAsync(clientFacing, egress, lockedClient, pumpCts.Token);
            Task tcpWatch = _UdpTcpControlWatcherAsync(_clientStream!, pumpCts);

            await Task.WhenAny(pumpC2T, pumpT2C, tcpWatch).ConfigureAwait(false);
            pumpCts.Cancel();
            // Best-effort drain — pumps will already be returning from cancellation.
            try { await Task.WhenAll(pumpC2T, pumpT2C, tcpWatch).ConfigureAwait(false); } catch { }
            _logger?.LogInformation("UDP ASSOCIATE session ended");
        }

        sealed class ClientEndpointBox
        {
            public IPEndPoint? Endpoint;
        }

        // Client → real target. Strip SOCKS5 UDP header, resolve DNS if ATYP=Domain, forward via egress.
        async Task _UdpClientToTargetPumpAsync(
            UdpClient clientFacing,
            IUdpAssociateSource egress,
            IPAddress allowedClientIp,
            int? requiredClientPort,
            ClientEndpointBox lockedClient,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
#if NET6_0_OR_GREATER
                    result = await clientFacing.ReceiveAsync(ct).ConfigureAwait(false);
#else
                    using (ct.Register(() => { try { clientFacing.Close(); } catch { } }))
                        result = await clientFacing.ReceiveAsync().ConfigureAwait(false);
#endif
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (SocketException ex)
                {
                    _logger?.LogDebug(ex, "UDP client-facing receive failed");
                    return;
                }

                IPEndPoint src = result.RemoteEndPoint;
                // Source filter: lock on first valid datagram; thereafter only accept that endpoint.
                if (lockedClient.Endpoint is null)
                {
                    if (!allowedClientIp.Equals(src.Address)) { _logger?.LogDebug("drop datagram from unexpected IP {Src}", src); continue; }
                    if (requiredClientPort.HasValue && requiredClientPort.Value != src.Port) { _logger?.LogDebug("drop datagram from unexpected port {Src}", src); continue; }
                    lockedClient.Endpoint = src;
                    _logger?.LogInformation("UDP client locked to {Src}", src);
                }
                else if (!lockedClient.Endpoint.Equals(src))
                {
                    _logger?.LogDebug("drop datagram from non-locked endpoint {Src}", src);
                    continue;
                }

                Socks5_UdpDatagram dgram;
                try { dgram = Socks5_UdpDatagram.Parse(result.Buffer); }
                catch (Exception ex) { _logger?.LogWarning(ex, "drop malformed SOCKS5 UDP datagram"); continue; }

                // Resolve destination — domain → DNS (best-effort, per-datagram, no cache).
                IPAddress destAddr;
                if (dgram.IPAddress is not null)
                {
                    destAddr = dgram.IPAddress;
                }
                else
                {
                    try
                    {
#if NET6_0_OR_GREATER
                        IPAddress[] ips = await Dns.GetHostAddressesAsync(dgram.Domain!, ct).ConfigureAwait(false);
#else
                        IPAddress[] ips = await Dns.GetHostAddressesAsync(dgram.Domain!).ConfigureAwait(false);
#endif
                        if (ips.Length == 0) { _logger?.LogDebug("DNS empty for {Host}", dgram.Domain); continue; }
                        destAddr = ips[0];
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "DNS resolve failed for {Host}", dgram.Domain);
                        continue;
                    }
                }

                try
                {
                    await egress.SendAsync(new IPEndPoint(destAddr, dgram.Port), dgram.Payload, 0, dgram.Payload.Length, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "egress send failed dst={Dst}:{Port}", destAddr, dgram.Port);
                }
            }
        }

        // Real target → client. Wrap reply with SOCKS5 UDP header and send to the locked client endpoint.
        async Task _UdpTargetToClientPumpAsync(
            UdpClient clientFacing,
            IUdpAssociateSource egress,
            ClientEndpointBox lockedClient,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                UdpAssociateDatagram reply;
                try
                {
                    reply = await egress.ReceiveAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "egress receive failed");
                    return;
                }

                IPEndPoint? client = lockedClient.Endpoint;
                if (client is null) { _logger?.LogDebug("drop reply — client endpoint not yet locked"); continue; }

                byte[] frame;
                try { frame = Socks5_UdpDatagram.Encode(reply.Source, reply.Payload, 0, reply.Payload.Length); }
                catch (Exception ex) { _logger?.LogDebug(ex, "encode reply failed"); continue; }

                try
                {
                    await clientFacing.SendAsync(frame, frame.Length, client).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "client-facing send failed to {Client}", client);
                }
            }
        }

        // Watch the TCP control connection — RFC 1928 §6 mandates teardown when it closes. A 0-byte
        // read from a NetworkStream means the peer FIN'd; cancel the CTS to unwind both UDP pumps.
        static async Task _UdpTcpControlWatcherAsync(Stream controlStream, CancellationTokenSource cts)
        {
            byte[] buf = new byte[64];
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    int n = await controlStream.ReadAsync(buf, 0, buf.Length, cts.Token).ConfigureAwait(false);
                    if (n == 0) break; // peer closed
                    // RFC says nothing meaningful comes on control after associate — discard any stray bytes.
                }
            }
            catch { /* socket closed / cancelled */ }
            finally
            {
                try { cts.Cancel(); } catch { }
            }
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

            await new StreamTransferHelper(clientStream, target_stream, _tunnelId, _loggerFactory)
                .DebugName(_clientEndPoint, listen_endpoint)
                .WaitUntilDisconnect(_cancellationToken);
        }
    }
}
