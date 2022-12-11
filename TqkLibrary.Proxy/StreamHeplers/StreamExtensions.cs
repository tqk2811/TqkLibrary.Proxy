using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal static class Extensions
    {
        internal static async Task TransferAsync(this Stream from, Stream to, long size, int bufferSize = 4096, CancellationToken cancellationToken = default)
        {
            if (size <= 0) return;
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (to == null) throw new ArgumentNullException(nameof(to));
            if (!from.CanRead) throw new InvalidOperationException($"{nameof(from)} must be {nameof(Stream.CanRead)}");
            if (!to.CanWrite) throw new InvalidOperationException($"{nameof(to)} must be {nameof(Stream.CanWrite)}");

            long totalRead = 0;
            byte[] buffer = new byte[bufferSize];
            do
            {
                int byte_read = await from.ReadAsync(buffer, 0, (int)Math.Min(bufferSize, size - totalRead)).ConfigureAwait(false);
                await to.WriteAsync(buffer, 0, byte_read).ConfigureAwait(false);
                totalRead += byte_read;
            }
            while (totalRead < size);
        }
    }
}
