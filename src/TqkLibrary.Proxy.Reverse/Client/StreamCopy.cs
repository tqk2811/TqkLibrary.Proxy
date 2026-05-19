namespace TqkLibrary.Proxy.Reverse.Client
{
    internal static class StreamCopy
    {
        public static async Task PumpAsync(Stream a, Stream b, CancellationToken ct)
        {
            try
            {
                var t1 = a.CopyToAsync(b, 81920, ct);
                var t2 = b.CopyToAsync(a, 81920, ct);
                await Task.WhenAny(t1, t2).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                try { a.Dispose(); } catch { }
                try { b.Dispose(); } catch { }
            }
        }
    }
}
