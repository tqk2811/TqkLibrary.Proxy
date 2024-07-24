using System.Text;

namespace TqkLibrary.Proxy.StreamHeplers
{
    public static class StreamExtensions
    {
        public static async Task TransferAsync(
            this Stream from,
            Stream to,
            long size,
            int bufferSize = 4096,
            CancellationToken cancellationToken = default
            )
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

        public static async Task<byte[]> ReadBytesAsync(
            this Stream from,
            int size,
            CancellationToken cancellationToken = default
            )
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

        public static async Task<byte> ReadByteAsync(
            this Stream from,
            CancellationToken cancellationToken = default
            )
        {
            byte[] bytes = await ReadBytesAsync(from, 1, cancellationToken);
            return bytes.First();
        }


        public static Task WriteAsync(
            this Stream stream, 
            string text, 
            CancellationToken cancellationToken = default
            )
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentNullException(nameof(text));
            byte[] buffer = Encoding.ASCII.GetBytes(text);
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }
        public static Task WriteLineAsync(
            this Stream stream, 
            string text, 
            CancellationToken cancellationToken = default
            )
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentNullException(nameof(text));
            byte[] buffer = new byte[text.Length + 2];
            buffer[text.Length] = 13;
            buffer[text.Length + 1] = 10;
            Encoding.ASCII.GetBytes(text, 0, text.Length, buffer, 0);
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }

        public static byte[] LineBreak => new byte[] { 13, 10 };
        public static async Task WriteLineAsync(
            this Stream stream, 
            CancellationToken cancellationToken = default
            )
        {
            await stream.WriteAsync(LineBreak, cancellationToken);
        }

        public static Task WriteHeadersAsync(
            this Stream stream, 
            IEnumerable<string> headers, 
            CancellationToken cancellationToken = default
            )
        {
            return stream.WriteLineAsync(string.Join("\r\n", headers) + "\r\n", cancellationToken);
        }


        public static async Task<string> ReadLineAsync(
            this Stream stream, 
            CancellationToken cancellationToken = default
            )
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

        public static async Task<byte[]> ReadUntilNullTerminated(
            this Stream stream, 
            int maxLength = 256 * 1024,
            CancellationToken cancellationToken = default
            )
        {
            using MemoryStream memoryStream = new MemoryStream();
            byte[] buffer = new byte[1];
            int totalRead = 0;
            while (true)
            {
                int byte_read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                if (byte_read == 0)
                {
                    throw new InvalidOperationException($"End of stream");
                }
                totalRead += byte_read;

                if (totalRead > maxLength) throw new InvalidOperationException($"Data too long");

                if (buffer[0] == 0)
                {
                    return memoryStream.ToArray();
                }
                else memoryStream.WriteByte(buffer[0]);
            }
        }


        public static Task WriteAsync(
            this Stream stream, 
            byte[] buffer, 
            CancellationToken cancellationToken = default
            )
        {
            return stream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }
    }
}
