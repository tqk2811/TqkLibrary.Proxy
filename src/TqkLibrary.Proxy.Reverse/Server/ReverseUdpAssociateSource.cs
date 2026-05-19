using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.Reverse.Protocol;

namespace TqkLibrary.Proxy.Reverse.Server
{
    internal sealed class ReverseUdpAssociateSource : ReverseSourceBase, IUdpAssociateSource
    {
        private PendingTunnel? _pending;

        public ReverseUdpAssociateSource(ReverseClientSession session, Guid tunnelId)
            : base(session, tunnelId) { }

        internal async Task RequestAsync(CancellationToken cancellationToken)
        {
            _pending = Session.CreatePending(TunnelId, cancellationToken);
            await Session.SendControlAsync(FrameType.OpenUdp, new OpenUdpPayload
            {
                TunnelId = TunnelId,
            }, cancellationToken).ConfigureAwait(false);
        }

        public new async Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
        {
            if (_pending is null)
                throw new InvalidOperationException("Source not initialized.");
            if (Stream is not null)
                return Stream;
            Stream = await _pending.DataStreamTcs.Task.ConfigureAwait(false);
            return Stream;
        }
    }
}
