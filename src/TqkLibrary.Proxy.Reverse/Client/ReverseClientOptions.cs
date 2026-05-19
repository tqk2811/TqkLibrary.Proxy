namespace TqkLibrary.Proxy.Reverse.Client
{
    public sealed class ReverseClientOptions
    {
        public string? ClientId { get; set; }
        public string? Token { get; set; }
        public string[]? Tags { get; set; }

        public bool SupportUdp { get; set; } = false;
        public bool SupportBind { get; set; } = false;
        public bool SupportIpv6 { get; set; } = true;

        /// <summary>How long to wait for HelloAck before treating the handshake as failed.</summary>
        public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>Idle keepalive interval. Set to <see cref="TimeSpan.Zero"/> to disable.</summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>True to keep retrying when the control channel drops; false to throw out of RunAsync.</summary>
        public bool AutoReconnect { get; set; } = true;
    }
}
