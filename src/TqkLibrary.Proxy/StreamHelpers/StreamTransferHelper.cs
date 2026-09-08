using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace TqkLibrary.Proxy.StreamHelpers
{
    public class StreamTransferHelper
    {
        const int BUFFER_SIZE = 4096;

        readonly ILogger? _logger;

        readonly Guid _tunnelId;

        readonly Stream _first;
        readonly byte[] _firstBuffer = new byte[BUFFER_SIZE];

        readonly Stream _second;
        readonly byte[] _secondBuffer = new byte[BUFFER_SIZE];
        public StreamTransferHelper(Stream first, Stream second, Guid tunnelId, ILoggerFactory? loggerFactory = null)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _tunnelId = tunnelId;
            _logger = loggerFactory?.CreateLogger<StreamTransferHelper>();
        }

        string _firstName = "first";
        string _secondName = "second";
        public StreamTransferHelper DebugName(object? first, object? second)
        {
            return DebugName(first?.ToString(), second?.ToString());
        }
        public StreamTransferHelper DebugName(string? firstName, string? secondName)
        {
            _firstName = firstName ?? "first";
            _secondName = secondName ?? "second";
            return this;
        }

        Task? _taskWork = null;

        /// <summary>
        /// Pumps both directions until the tunnel is done, then completes.
        /// </summary>
        /// <remarks>
        /// The two directions are not independent, which is the whole difficulty. One of them
        /// reaching EOF means the near side has stopped sending, and the far side has to be told
        /// so — otherwise it sits on a connection nobody will ever write to again, and the pump
        /// facing it stays parked in a read for as long as that side's idle timeout, which for a
        /// keep-alive server can be forever. An error on either side is stronger still: it ends
        /// both directions, and the only way to wake a read already parked is to close its stream.
        /// </remarks>
        public Task WaitUntilDisconnect(CancellationToken cancellationToken = default)
        {
            if (_taskWork is not null)
                return _taskWork;

            // Cancelling has to reach reads that are already parked. On net6+ the token does that
            // by itself, but on netstandard2.0 NetworkStream.ReadAsync ignores it outright and
            // closing the stream is the only thing that ends the wait.
            CancellationTokenRegistration registration = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(static s => ((StreamTransferHelper)s!).CloseBoth(), this)
                : default;

            Task task_first = PumpAsync(_first, _second, _firstBuffer, forward: true, cancellationToken);
            Task task_second = PumpAsync(_second, _first, _secondBuffer, forward: false, cancellationToken);
            _taskWork = WhenBothFinish(task_first, task_second, registration);
            return _taskWork;
        }

        static async Task WhenBothFinish(Task first, Task second, CancellationTokenRegistration registration)
        {
            try { await Task.WhenAll(first, second).ConfigureAwait(false); }
            finally { registration.Dispose(); }
        }

        IDisposable? BeginTunnelScope() => _logger?.BeginScope("TunnelId:{TunnelId}", _tunnelId);

        async Task PumpAsync(Stream from, Stream to, byte[] buffer, bool forward, CancellationToken cancellationToken)
        {
            using var scope = BeginTunnelScope();
            try
            {
                while (true)
                {
                    if (!from.CanRead) break;
                    int byte_read = await from.ReadAsync(buffer, 0, BUFFER_SIZE, cancellationToken).ConfigureAwait(false);
                    if (!to.CanWrite) break;

                    if (forward) _logger?.LogTrace("[{First} -> {Second}] {Bytes} bytes", _firstName, _secondName, byte_read);
                    else _logger?.LogTrace("[{First} <- {Second}] {Bytes} bytes", _firstName, _secondName, byte_read);

                    if (byte_read <= 0) break;
                    await to.WriteAsync(buffer, 0, byte_read, cancellationToken).ConfigureAwait(false);
                }

                // Reaching here means this side sent everything it is going to. Pass that on as a
                // half-close: a protocol that answers only once its request is complete needs to
                // see it, and without it the far end has no reason to ever close.
                HalfClose(to);
            }
            catch (Exception ex)
            {
                if (forward) _logger?.LogInformation(ex, "[{First} -> {Second}]", _firstName, _secondName);
                else _logger?.LogInformation(ex, "[{First} <- {Second}]", _firstName, _secondName);

                // A fault is not a half-close: nothing more is coming either way, and the other
                // pump is parked in a read that only closing its stream will end.
                CloseBoth();
            }
        }

        /// <summary>
        /// Tells the far end that nothing more will be sent, without touching what it still owes
        /// us. Streams that cannot express a half-close are left alone rather than closed, since
        /// closing one here would cut off data the other direction is still carrying.
        /// </summary>
        static void HalfClose(Stream stream)
        {
            try
            {
                if (stream is NetworkStream networkStream)
                    SocketOf(networkStream)?.Shutdown(SocketShutdown.Send);
            }
            catch { /* already gone: the far end learns of it either way */ }
        }

#if NET6_0_OR_GREATER
        static Socket? SocketOf(NetworkStream stream) => stream.Socket;
#else
        /// <remarks>
        /// <c>NetworkStream.Socket</c> only became public in .NET Core 3.0. On the frameworks this
        /// package still targets it is protected, and an instance handed to us from outside cannot
        /// be reached any other way.
        /// </remarks>
        static readonly System.Reflection.PropertyInfo? _networkStreamSocket = typeof(NetworkStream)
            .GetProperty("Socket", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        static Socket? SocketOf(NetworkStream stream)
        {
            try { return _networkStreamSocket?.GetValue(stream) as Socket; }
            catch { return null; }
        }
#endif

        void CloseBoth()
        {
            try { _first.Dispose(); } catch { }
            try { _second.Dispose(); } catch { }
        }
    }
}
