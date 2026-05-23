using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshNet.Exceptions;

namespace TqkLibrary.Proxy.SshNet
{
    public class SshNetConnectSource : IConnectSource
    {
        private readonly SshClient _client;
        private readonly SshNetConnectionOptions _options;
        private readonly ILogger? _logger;
        private ForwardedPortLocal? _port;
        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private int _disposed;

        internal SshNetConnectSource(SshClient client, SshNetConnectionOptions options, ILoggerFactory? loggerFactory)
        {
            _client = client;
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

            ForwardedPortLocal port;
            try
            {
                port = new ForwardedPortLocal(_options.LocalBindHost, 0, address.Host, (uint)address.Port);
                _client.AddForwardedPort(port);
                port.Start();
            }
            catch (Exception ex)
            {
                throw new InitConnectSourceFailedException($"Failed to start ssh local forwarder: {ex.Message}");
            }

            try
            {
                var tcp = new TcpClient();
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linked.CancelAfter(_options.ConnectProbeTimeoutMs);
                    try
                    {
#if NET6_0_OR_GREATER
                        await tcp.ConnectAsync(_options.LocalBindHost, (int)port.BoundPort, linked.Token).ConfigureAwait(false);
#else
                        var connectTask = tcp.ConnectAsync(_options.LocalBindHost, (int)port.BoundPort);
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
                            $"Timed out connecting to ssh local forwarder for {address.Host}:{address.Port}.");
                    }
                }

                _tcp = tcp;
                _stream = tcp.GetStream();
                _port = port;
            }
            catch
            {
                try { port.Stop(); } catch { }
                try { _client.RemoveForwardedPort(port); } catch { }
                try { port.Dispose(); } catch { }
                throw;
            }
        }

        public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
        {
            CheckDisposed();
            if (_stream is null)
                throw new InvalidOperationException($"Must call {nameof(ConnectAsync)} first.");
            return Task.FromResult<Stream>(_stream);
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
            if (_port != null)
            {
                try { _port.Stop(); } catch { }
                try { _client.RemoveForwardedPort(_port); } catch { }
                try { _port.Dispose(); } catch { }
            }
        }
    }
}
