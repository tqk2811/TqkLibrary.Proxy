using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshNet.Exceptions;

namespace TqkLibrary.Proxy.SshNet
{
    /// <summary>
    /// One tunnel: a direct-tcpip channel on the shared session, reached through a loopback
    /// forwarder of its own.
    /// </summary>
    /// <remarks>
    /// The forwarder is there because SSH.NET keeps its direct-tcpip channel internal —
    /// <see cref="ForwardedPortLocal"/> is the only public way to open one. That leaves a listening
    /// loopback port for the life of the tunnel, and any process on the machine could connect to it
    /// and be forwarded through the user's SSH session. So this source binds its own socket first,
    /// knows the source port it will connect from, and refuses every other originator in
    /// <see cref="ForwardedPort.RequestReceived"/> — which SSH.NET raises before it opens the channel,
    /// closing the socket of an originator whose handler throws.
    /// <para>
    /// A destination the server cannot reach is reported late: the loopback connect has already
    /// succeeded by then, so the failure arrives as the stream closing rather than as an exception
    /// from <see cref="ConnectAsync"/>. The reason is logged from the forwarder's own error event.
    /// </para>
    /// </remarks>
    public class SshNetConnectSource : IConnectSource
    {
        private readonly SshClient _client;
        private readonly object _portsLock;
        private readonly SshNetConnectionOptions _options;
        private readonly ILogger? _logger;
        private ForwardedPortLocal? _port;
        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private int _ownPort;
        private int _accepted;
        private int _threadHeld;
        private int _disposed;

        internal SshNetConnectSource(
            SshClient client, object portsLock, SshNetConnectionOptions options, ILoggerFactory? loggerFactory)
        {
            _client = client;
            _portsLock = portsLock;
            _options = options;
            _logger = loggerFactory?.CreateLogger<SshNetConnectSource>();
        }

        public async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            CheckDisposed();
            if (_port != null) throw new InvalidOperationException("Already connected.");

            if (!_client.IsConnected)
                throw new InitConnectSourceFailedException("Underlying SSH client is not connected.");

            // Uri.Host keeps the brackets of an IPv6 literal ("[2606:4700::1111]"). The server passes
            // the direct-tcpip host to getaddrinfo, which reads brackets as part of a name and fails.
            string host = address.Host.Trim('[', ']');

            // Bound before the forwarder exists, so the one originator it may accept is known by the
            // time the first connection reaches it. Exclusive so no other socket can take the same
            // loopback address and port and pass as this one.
            IPAddress bindAddress = ParseLoopback(_options.LocalBindHost);
            var tcp = new TcpClient(bindAddress.AddressFamily);
            try
            {
                tcp.Client.ExclusiveAddressUse = true;
                tcp.Client.Bind(new IPEndPoint(bindAddress, 0));
                _ownPort = ((IPEndPoint)tcp.Client.LocalEndPoint!).Port;
            }
            catch (Exception ex)
            {
                try { tcp.Dispose(); } catch { }
                throw new InitConnectSourceFailedException($"Failed to bind the loopback socket for the ssh forwarder: {ex.Message}");
            }

            ForwardedPortLocal port;
            try
            {
                port = new ForwardedPortLocal(bindAddress.ToString(), 0, host, (uint)address.Port);
                port.RequestReceived += OnRequestReceived;
                port.Exception += OnForwarderException;
                lock (_portsLock)
                {
                    _client.AddForwardedPort(port);
                    port.Start();
                }
            }
            catch (Exception ex)
            {
                try { tcp.Dispose(); } catch { }
                throw new InitConnectSourceFailedException($"Failed to start ssh local forwarder: {ex.Message}");
            }

            // Taken before the connect, because the accept it triggers is what occupies the thread —
            // see BlockedThreadReservation — and given back in Dispose or on the way out below.
            HoldThread();
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linked.CancelAfter(_options.ConnectProbeTimeoutMs);
                    try
                    {
#if NET6_0_OR_GREATER
                        await tcp.ConnectAsync(bindAddress, (int)port.BoundPort, linked.Token).ConfigureAwait(false);
#else
                        var connectTask = tcp.ConnectAsync(bindAddress, (int)port.BoundPort);
                        var completed = await Task.WhenAny(connectTask, Task.Delay(Timeout.Infinite, linked.Token)).ConfigureAwait(false);
                        if (completed != connectTask)
                        {
                            try { tcp.Close(); } catch { }
                            linked.Token.ThrowIfCancellationRequested();
                        }
                        await connectTask.ConfigureAwait(false);
#endif
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        try { tcp.Close(); } catch { }
                        throw new InitConnectSourceFailedException(
                            $"Timed out connecting to ssh local forwarder for {host}:{address.Port}.");
                    }
                }

                _tcp = tcp;
                _stream = tcp.GetStream();
                _port = port;
            }
            catch
            {
                try { tcp.Dispose(); } catch { }
                ReleasePort(port);
                ReleaseThread();
                throw;
            }
        }

        private void HoldThread()
        {
            if (Interlocked.Exchange(ref _threadHeld, 1) == 0) BlockedThreadReservation.Acquire();
        }

        private void ReleaseThread()
        {
            if (Interlocked.Exchange(ref _threadHeld, 0) == 1) BlockedThreadReservation.Release();
        }

        public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (_stream is null)
                throw new InvalidOperationException($"Must call {nameof(ConnectAsync)} first.");
            return Task.FromResult<Stream>(_stream);
        }

        // Raised on the forwarder's accept thread, before the channel is opened. Throwing here makes
        // SSH.NET close the accepted socket without forwarding anything.
        private void OnRequestReceived(object? sender, PortForwardEventArgs e)
        {
            bool ours = e.OriginatorPort == (uint)_ownPort
                && IPAddress.TryParse(e.OriginatorHost, out IPAddress? origin)
                && IPAddress.IsLoopback(origin);
            // Once only: the tunnel is one connection, so a second one from anywhere is not it.
            if (ours && Interlocked.Exchange(ref _accepted, 1) == 0) return;

            throw new SshNetException(
                $"Refused a connection to the ssh forwarder from {e.OriginatorHost}:{e.OriginatorPort}: "
                + "only this tunnel's own socket may use it.");
        }

        private void OnForwarderException(object? sender, ExceptionEventArgs e)
        {
            if (e.Exception is SshNetException)
                _logger?.LogWarning("{Message}", e.Exception.Message);
            else
                _logger?.LogDebug(e.Exception, "ssh forwarder reported an error");
        }

        private void ReleasePort(ForwardedPortLocal port)
        {
            port.RequestReceived -= OnRequestReceived;
            port.Exception -= OnForwarderException;
            // Stop waits for the port's channel to finish, which can take up to the session timeout,
            // so it stays outside the lock: holding it there would queue every other tunnel opening or
            // closing behind this one. Only the client's list needs the lock.
            try { port.Stop(); } catch { }
            lock (_portsLock)
            {
                try { _client.RemoveForwardedPort(port); } catch { }
            }
            try { port.Dispose(); } catch { }
        }

        private static IPAddress ParseLoopback(string host)
        {
            if (IPAddress.TryParse(host.Trim('[', ']'), out IPAddress? address) && IPAddress.IsLoopback(address))
                return address;
            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
                return IPAddress.Loopback;
            throw new InitConnectSourceFailedException(
                $"LocalBindHost must be a loopback address, not '{host}': the forwarder would be reachable from the network.");
        }

        private void CheckDisposed()
        {
            if (_disposed != 0) throw new ObjectDisposedException(nameof(SshNetConnectSource));
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
            try { _tcp?.Dispose(); } catch { }
            if (_port != null) ReleasePort(_port);
            // After the port has stopped, which waits for the channel's read loop to end: that loop
            // is the thread this was holding.
            ReleaseThread();
        }
    }
}
