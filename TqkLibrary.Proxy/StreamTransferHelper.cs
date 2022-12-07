﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy
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
            this._first = first ?? throw new ArgumentNullException(nameof(first));
            this._second = second ?? throw new ArgumentNullException(nameof(second));
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
                    int byte_read = await _first.ReadAsync(_firstBuffer, 0, BUFFER_SIZE, cancellationToken).ConfigureAwait(false);
#if DEBUG
                    Console.WriteLine($"_first.ReadAsync: {byte_read}bytes");
#endif
                    if (byte_read > 0) await _second.WriteAsync(_firstBuffer, 0, byte_read, cancellationToken).ConfigureAwait(false);
                    else return;
                }
            }
            catch(Exception ex)
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
                    Console.WriteLine($"_second.ReadAsync: {byte_read}bytes");
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
