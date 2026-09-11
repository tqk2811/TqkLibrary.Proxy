namespace TqkLibrary.Proxy.SshNet
{
    /// <summary>
    /// Keeps one thread-pool thread in hand for every tunnel that is holding one.
    /// </summary>
    /// <remarks>
    /// SSH.NET forwards a <see cref="Renci.SshNet.ForwardedPortLocal"/> connection by opening the
    /// channel and then running its blocking read loop (<c>ChannelDirectTcpip.Bind</c>) on the very
    /// thread-pool thread that completed the accept — for the whole life of the tunnel. With the
    /// pool at its default minimum (the processor count), a browser opening a few dozen connections
    /// at once takes every thread, the pool then adds one about every half second, and meanwhile
    /// nothing else in the process runs: measured, 40 simultaneous tunnels timed out against a
    /// minimum of 32 threads and completed in 200 ms with the minimum raised.
    /// <para>
    /// So the minimum is raised by one while each tunnel lives and lowered again when it closes —
    /// never below what it was when the first tunnel came, so a value some other part of the process
    /// set is left alone. Only worker threads: that is where the accept completion runs.
    /// </para>
    /// </remarks>
    internal static class BlockedThreadReservation
    {
        private static readonly object _lock = new object();
        private static int _held;
        private static int _baseline;

        public static void Acquire()
        {
            lock (_lock)
            {
                ThreadPool.GetMinThreads(out int worker, out int io);
                if (_held == 0) _baseline = worker;
                _held++;
                ThreadPool.SetMinThreads(worker + 1, io);
            }
        }

        public static void Release()
        {
            lock (_lock)
            {
                if (_held == 0) return;
                _held--;
                ThreadPool.GetMinThreads(out int worker, out int io);
                ThreadPool.SetMinThreads(Math.Max(_baseline, worker - 1), io);
            }
        }
    }
}
