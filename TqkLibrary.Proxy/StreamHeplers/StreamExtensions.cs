using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal static class StreamExtensions
    {
        internal static async Task TransferAsync(this Stream from,
            Stream to,
            long size,
            int bufferSize = 4096,
            CancellationToken cancellationToken = default)
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
                int byte_read = await from.ReadAsync(buffer, 0, (int)Math.Min(bufferSize, size - totalRead), cancellationToken);
                await to.WriteAsync(buffer, 0, byte_read, cancellationToken);
                totalRead += byte_read;
            }
            while (totalRead < size);
        }

        internal static async Task<byte[]> ReadBytesAsync(
            this Stream from,
            int size,
            CancellationToken cancellationToken = default)
        {
            if (size <= 0) return new byte[0];
            if (from == null) throw new ArgumentNullException(nameof(from));
            if (!from.CanRead) throw new InvalidOperationException($"{nameof(from)} must be {nameof(Stream.CanRead)}");
            if (size > Singleton.ContentMaxLength) throw new InvalidOperationException("Content too long");

            int totalRead = 0;
            byte[] buffer = new byte[size];
            do
            {
                int byte_read = await from.ReadAsync(buffer, totalRead, size - totalRead, cancellationToken);
                if (byte_read == 0) throw new InvalidOperationException("Stream ended");
                totalRead += byte_read;
            }
            while (totalRead < size);
            return buffer;
        }


        internal static Task WriteAsync(this Stream stream, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentNullException(nameof(text));
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }
        internal static Task WriteLineAsync(this Stream stream, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentNullException(nameof(text));
            byte[] buffer = new byte[text.Length + 2];
            buffer[text.Length] = 13;
            buffer[text.Length + 1] = 10;
            Encoding.ASCII.GetBytes(text, 0, text.Length, buffer, 0);
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        static readonly byte[] line_break = new byte[] { 13, 10 };
        internal static Task WriteLineAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            return stream.WriteAsync(line_break, 0, line_break.Length, cancellationToken);
        }




        internal static async Task<string> ReadLineAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            using MemoryStream memoryStream = new MemoryStream();
            byte[] buffer = new byte[1];
            int totalRead = 0;
            while (true)
            {
                int byte_read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (byte_read == 0)
                {
                    if (totalRead == 0)
                        return string.Empty;
                    else
                        throw new InvalidOperationException();
                }
                totalRead += byte_read;

                if (totalRead > Singleton.HeaderMaxLength) throw new InvalidOperationException();

                if (buffer[0] == 13)
                {
                    byte_read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (byte_read == 0)
                        throw new InvalidOperationException();
                    if (buffer[0] == 10)
                        return Encoding.ASCII.GetString(memoryStream.ToArray());
                    else
                        throw new InvalidOperationException();
                }
                else memoryStream.WriteByte(buffer[0]);
            }
        }
    }
}
