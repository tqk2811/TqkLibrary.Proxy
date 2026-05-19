using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.Reverse.Protocol;

namespace TqkLibrary.Proxy.Reverse.Server
{
    internal sealed class ReverseConnectSource : ReverseSourceBase, IConnectSource
    {
        public ReverseConnectSource(ReverseClientSession session, Guid tunnelId)
            : base(session, tunnelId) { }

        public async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));

            var pending = Session.CreatePending(TunnelId, cancellationToken);
            await Session.SendControlAsync(FrameType.OpenConnect, new OpenConnectPayload
            {
                TunnelId = TunnelId,
                Host = address.IdnHost,
                Port = address.Port,
            }, cancellationToken).ConfigureAwait(false);

            Stream = await pending.DataStreamTcs.Task.ConfigureAwait(false);
        }
    }
}
