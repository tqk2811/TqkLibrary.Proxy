using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Reverse.Server
{
    internal abstract class ReverseSourceBase : IBaseSource
    {
        protected readonly ReverseClientSession Session;
        protected readonly Guid TunnelId;
        protected Stream? Stream;
        private int _disposed;

        protected ReverseSourceBase(ReverseClientSession session, Guid tunnelId)
        {
            Session = session;
            TunnelId = tunnelId;
        }

        public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
        {
            if (Stream is null)
                throw new InvalidOperationException("Stream is not yet available; call ConnectAsync/BindAsync first.");
            return Task.FromResult(Stream);
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try { Stream?.Dispose(); } catch { }
            Session.RemovePending(TunnelId);
            try { _ = Session.SendTunnelCloseSafeAsync(TunnelId); } catch { }
        }
    }
}
