using System.Net;

namespace TqkLibrary.Proxy.Reverse.Server
{
    internal sealed class PendingTunnel
    {
        public TaskCompletionSource<Stream> DataStreamTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<IPEndPoint> BindReadyTcs { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationTokenRegistration CtReg { get; set; }

        public void Fail(Exception ex)
        {
            DataStreamTcs.TrySetException(ex);
            BindReadyTcs.TrySetException(ex);
        }
    }
}
