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
#if DEBUG
        string _firstName = string.Empty;
        string _secondName = string.Empty;

        public StreamTransferHelper DebugName(string firstName, string secondName)
        {
            this._firstName = firstName ?? string.Empty;
            this._secondName = secondName ?? string.Empty;
            return this;
        }

#endif
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
                    int byte_read = await _first.ReadAsync(_firstBuffer, 0, BUFFER_SIZE, cancellationToken).ConfigureAwait(false);
#if DEBUG
                    Console.WriteLine($"[{_firstName} >> {_secondName}] {byte_read} bytes");
#endif
                    if (byte_read > 0) await _second.WriteAsync(_firstBuffer, 0, byte_read, cancellationToken).ConfigureAwait(false);
                    else return;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($"{ex.GetType().FullName}: {ex.Message}");
#endif
            }
        }
        async Task SecondToFirst(CancellationToken cancellationToken = default)
        {
            try
            {
                while (true)
                {
                    int byte_read = await _second.ReadAsync(_secondBuffer, 0, BUFFER_SIZE, cancellationToken).ConfigureAwait(false);
#if DEBUG
                    Console.WriteLine($"[{_firstName} << {_secondName}] {byte_read} bytes");
#endif
                    if (byte_read > 0) await _first.WriteAsync(_secondBuffer, 0, byte_read, cancellationToken).ConfigureAwait(false);
                    else return;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($"{ex.GetType().FullName}: {ex.Message}");
#endif
            }
        }


    }
}
