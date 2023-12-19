using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal class StreamTransferHelper
    {
        readonly ILogger<StreamTransferHelper>? _logger = Singleton.LoggerFactory?.CreateLogger<StreamTransferHelper>();

        const int BUFFER_SIZE = 4096;

        readonly Stream _first;
        readonly byte[] _firstBuffer = new byte[BUFFER_SIZE];

        readonly Stream _second;
        readonly byte[] _secondBuffer = new byte[BUFFER_SIZE];
        public StreamTransferHelper(Stream first, Stream second)
        {
            _first = first ?? throw new ArgumentNullException(nameof(first));
            _second = second ?? throw new ArgumentNullException(nameof(second));
        }

        string _firstName = string.Empty;
        string _secondName = string.Empty;
        public StreamTransferHelper DebugName(object? first, object? second)
        {
            return DebugName(first?.ToString(), second?.ToString());
        }
        public StreamTransferHelper DebugName(string? firstName, string? secondName)
        {
            this._firstName = firstName ?? string.Empty;
            this._secondName = secondName ?? string.Empty;
            return this;
        }

        public Task WaitUntilDisconnect(CancellationToken cancellationToken = default)
        {
            Task task_first = FirstToSecond(cancellationToken);
            Task task_second = SecondToFirst(cancellationToken);
            return Task.WhenAny(task_first, task_second);
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

                    _logger?.LogInformation($"[{_firstName} -> {_secondName}] {byte_read} bytes");

                    if (byte_read > 0) await _second.WriteAsync(_firstBuffer, 0, byte_read, cancellationToken);
                    else return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, $"[{_firstName} -> {_secondName}]");
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

                    _logger?.LogInformation($"[{_firstName} <- {_secondName}] {byte_read} bytes");

                    if (byte_read > 0) await _first.WriteAsync(_secondBuffer, 0, byte_read, cancellationToken);
                    else return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogInformation(ex, $"[{_firstName} <- {_secondName}]");
            }
        }


    }
}
