using Microsoft.Extensions.Logging;

namespace TqkLibrary.Proxy.StreamHeplers
{
    public class StreamTransferHelper : BaseLogger
    {
        const int BUFFER_SIZE = 4096;

        readonly Guid _tunnelId;

        readonly Stream _first;
        readonly byte[] _firstBuffer = new byte[BUFFER_SIZE];

        readonly Stream _second;
        readonly byte[] _secondBuffer = new byte[BUFFER_SIZE];
        public StreamTransferHelper(Stream first, Stream second, Guid tunnelId)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
            _tunnelId = tunnelId;
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
        public Task WaitUntilDisconnect(CancellationToken cancellationToken = default)
        {
            if (_taskWork is not null) 
                return _taskWork;

            Task task_first = FirstToSecond(cancellationToken);
            Task task_second = SecondToFirst(cancellationToken);
            _taskWork = Task.WhenAll(task_first, task_second);
            return _taskWork;
        }

        async Task FirstToSecond(CancellationToken cancellationToken = default)
        {
            try
            {
                while (true)
                {
                    if (!_first.CanRead) return;
                    int byte_read = await _first.ReadAsync(_firstBuffer, 0, BUFFER_SIZE, cancellationToken);
                    if (!_second.CanWrite) return;

                    _logger?.LogInformation($"{_tunnelId} [{_firstName} -> {_secondName}] {byte_read} bytes");

                    if (byte_read > 0) await _second.WriteAsync(_firstBuffer, 0, byte_read, cancellationToken);
                    else return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, $"{_tunnelId} [{_firstName} -> {_secondName}]");
            }
        }
        async Task SecondToFirst(CancellationToken cancellationToken = default)
        {
            try
            {
                while (true)
                {
                    if (!_second.CanRead) return;
                    int byte_read = await _second.ReadAsync(_secondBuffer, 0, BUFFER_SIZE, cancellationToken);
                    if (!_first.CanWrite) return;

                    _logger?.LogInformation($"{_tunnelId} [{_firstName} <- {_secondName}] {byte_read} bytes");

                    if (byte_read > 0) await _first.WriteAsync(_secondBuffer, 0, byte_read, cancellationToken);
                    else return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, $"{_tunnelId} [{_firstName} <- {_secondName}]");
            }
        }


    }
}
