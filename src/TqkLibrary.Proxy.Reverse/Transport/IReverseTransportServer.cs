namespace TqkLibrary.Proxy.Reverse.Transport
{
    /// <summary>
    /// Server-side transport. Responsible for accepting both control-channels and data-streams from clients.
    /// </summary>
    public interface IReverseTransportServer : IAsyncDisposable
    {
        /// <summary>Fired when a client opens a new control connection. Auth has NOT been performed yet.</summary>
        event Func<IControlChannel, CancellationToken, Task>? ControlChannelAccepted;

        /// <summary>
        /// Fired when a client opens a new data connection. The first frame on the stream is expected to be a DataHello.
        /// The transport hands the raw stream to consumers; the consumer is responsible for routing to the right session.
        /// </summary>
        event Func<Stream, CancellationToken, Task>? DataChannelAccepted;

        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
