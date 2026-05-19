using System.Net;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.Reverse.Protocol;

namespace TqkLibrary.Proxy.Reverse.Server
{
    internal sealed class ReverseUdpAssociateSource : ReverseSourceBase, IUdpAssociateSource
    {
        private const string NotImplementedMessage =
            "Reverse-tunneled UDP ASSOCIATE is not implemented: the reverse data channel has no datagram framing wire format. "
            + "Implement framing in ReverseClientHandler.DialUdpAsync and pair it with Send/Receive here.";

        private PendingTunnel? _pending;

        public IPEndPoint? RelayEndPoint => null;

        public IPEndPoint? LocalEndPoint => null;

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

        public Task<IPEndPoint> AssociateAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException(NotImplementedMessage);

        public Task SendAsync(IPEndPoint destination, byte[] payload, int offset, int count, CancellationToken cancellationToken = default)
            => throw new NotImplementedException(NotImplementedMessage);

        public Task<UdpAssociateDatagram> ReceiveAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException(NotImplementedMessage);

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
