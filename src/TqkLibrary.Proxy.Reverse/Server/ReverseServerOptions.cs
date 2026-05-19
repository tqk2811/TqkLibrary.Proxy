namespace TqkLibrary.Proxy.Reverse.Server
{
    public sealed class ReverseServerOptions
    {
        /// <summary>Authenticate a client's Hello frame. Default: accepts everything (override in production).</summary>
        public Func<Protocol.HelloPayload, CancellationToken, Task<bool>>? Authenticate { get; set; }

        /// <summary>Callback fired after a session is fully accepted (handshake done). Runs before the event.</summary>
        public Func<ReverseClientSession, CancellationToken, Task>? OnClientConnected { get; set; }

        /// <summary>Callback fired when a session terminates.</summary>
        public Func<ReverseClientSession, Exception?, Task>? OnClientDisconnected { get; set; }

        /// <summary>Timeout to wait for the initial Hello frame after a control channel is accepted.</summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>Timeout to wait for the DataHello frame on each new data stream.</summary>
        public TimeSpan DataHelloTimeout { get; set; } = TimeSpan.FromSeconds(15);
    }
}
