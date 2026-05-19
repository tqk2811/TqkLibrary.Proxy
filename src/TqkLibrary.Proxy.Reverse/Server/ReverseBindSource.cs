using System.Net;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.Reverse.Protocol;

namespace TqkLibrary.Proxy.Reverse.Server
{
    internal sealed class ReverseBindSource : ReverseSourceBase, IBindSource
    {
        private PendingTunnel? _pending;

        public ReverseBindSource(ReverseClientSession session, Guid tunnelId)
            : base(session, tunnelId) { }

        public async Task<IPEndPoint> BindAsync(CancellationToken cancellationToken = default)
        {
            _pending = Session.CreatePending(TunnelId, cancellationToken);
            await Session.SendControlAsync(FrameType.OpenBind, new OpenBindPayload
            {
                TunnelId = TunnelId,
            }, cancellationToken).ConfigureAwait(false);

            return await _pending.BindReadyTcs.Task.ConfigureAwait(false);
        }

        public new async Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
        {
            if (_pending is null)
                throw new InvalidOperationException("BindAsync must be called first.");
            if (Stream is not null)
                return Stream;
            Stream = await _pending.DataStreamTcs.Task.ConfigureAwait(false);
            return Stream;
        }
    }
}
