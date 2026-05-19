namespace TqkLibrary.Proxy.Reverse.Transport
{
    /// <summary>
    /// Client-side transport. Connects to the server's control endpoint and opens additional data streams on demand.
    /// </summary>
    public interface IReverseTransportClient
    {
        Task<IControlChannel> ConnectControlAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Open a fresh stream to the server for one tunnel. Caller writes a DataHello frame as first bytes.
        /// </summary>
        Task<Stream> OpenDataAsync(CancellationToken cancellationToken = default);
    }
}
