using System.Globalization;
using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy
{
    internal static class HttpUtilities
    {
        internal static int GetContentLength(this IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("content-length:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(line.Substring("content-length:".Length).Trim(), out int result))
                    {
                        return result;
                    }
                }
            }
            return 0;
        }

        /// <summary>True when the headers carry a Content-Length at all, whatever its value.</summary>
        /// <remarks>
        /// "No Content-Length" and "Content-Length: 0" mean completely different things about the
        /// body that follows, and <see cref="GetContentLength"/> answers zero to both.
        /// </remarks>
        internal static bool HasContentLength(this IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (line.StartsWith("content-length:", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(line.Substring("content-length:".Length).Trim(), out _))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the body arrives as a series of chunks rather than as one known length.</summary>
        internal static bool IsChunked(this IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                if (!line.StartsWith("transfer-encoding:", StringComparison.OrdinalIgnoreCase)) continue;

                // The header is a list and chunked is always the last item applied, so it is enough
                // that it is mentioned: "gzip, chunked".
                return line.Substring("transfer-encoding:".Length)
                    .IndexOf("chunked", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        /// <summary>The status code of a response, or 0 when the first line is not one.</summary>
        internal static int GetStatusCode(this IReadOnlyList<string> lines)
        {
            if (lines.Count == 0) return 0;

            string[] parts = lines[0].Split(' ');
            return parts.Length >= 2 && int.TryParse(parts[1], out int code) ? code : 0;
        }

        /// <summary>
        /// Copies a chunked body through, framing and all, up to and including the terminating
        /// zero-length chunk and its trailer.
        /// </summary>
        /// <remarks>
        /// Forwarded verbatim rather than decoded: the headers going to the client still say
        /// chunked, so the framing has to reach it exactly as it arrived.
        /// </remarks>
        internal static async Task TransferChunkedAsync(
            this Stream from, Stream to, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                string sizeLine = await from.ReadLineAsync(cancellationToken);

                // The size may be followed by chunk extensions after a ';', which are passed on
                // with the rest of the line.
                string sizeText = sizeLine.Split(';')[0].Trim();
                if (!int.TryParse(sizeText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int size) || size < 0)
                    throw new InvalidDataException($"A chunk size of '{sizeLine}' is not a number.");

                await to.WriteLineAsync(sizeLine.Length > 0 ? sizeLine : "0", cancellationToken);

                if (size == 0) break;

                await from.TransferAsync(to, size, cancellationToken: cancellationToken);
                // The CRLF that closes the chunk. Read as a line so a malformed one is caught here
                // rather than being mistaken for the next chunk's size.
                await from.ReadLineAsync(cancellationToken);
                await to.WriteLineAsync(cancellationToken);
            }

            // Trailing headers, then the blank line that ends the message. Usually there are none
            // and this is a single empty line.
            while (true)
            {
                string trailer = await from.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(trailer))
                {
                    await to.WriteLineAsync(cancellationToken);
                    break;
                }
                await to.WriteLineAsync(trailer, cancellationToken);
            }
        }

        internal static async Task<IReadOnlyList<string>> ReadHeadersAsync(this Stream stream, CancellationToken cancellationToken = default)
        {
            List<string> lines = new List<string>();
            while (true)
            {
                //if (streamReader.EndOfStream)
                //    break;

                string line = await stream.ReadLineAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(line))
                {
                    break;
                }
                else
                {
                    lines.Add(line);
                    if (lines.Sum(x => x.Length) > Singleton.HeaderMaxLength)
                        throw new InvalidDataException("Header too long");
                }
            }
            return lines;
        }
    }
}
