using TqkLibrary.Proxy.StreamHeplers;

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
