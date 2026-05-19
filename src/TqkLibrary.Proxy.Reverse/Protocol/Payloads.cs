using System.Net;

namespace TqkLibrary.Proxy.Reverse.Protocol
{
    public sealed class HelloPayload
    {
        public string? ClientId { get; set; }
        public string? Token { get; set; }
        public string[]? Tags { get; set; }
        public bool SupportUdp { get; set; }
        public bool SupportBind { get; set; }
        public bool SupportIpv6 { get; set; }
        /// <summary>HMAC over server nonce (optional). Empty when not used.</summary>
        public string? AuthSignature { get; set; }
    }

    public sealed class HelloAckPayload
    {
        public string? ServerId { get; set; }
        public Guid SessionId { get; set; }
    }

    public sealed class HelloDenyPayload
    {
        public string? Reason { get; set; }
    }

    public sealed class OpenConnectPayload
    {
        public Guid TunnelId { get; set; }
        public string Host { get; set; } = "";
        public int Port { get; set; }
    }

    public sealed class OpenBindPayload
    {
        public Guid TunnelId { get; set; }
    }

    public sealed class OpenUdpPayload
    {
        public Guid TunnelId { get; set; }
    }

    public sealed class TunnelOpenedPayload
    {
        public Guid TunnelId { get; set; }
    }

    public sealed class TunnelErrorPayload
    {
        public Guid TunnelId { get; set; }
        public string? Code { get; set; }
        public string? Message { get; set; }
    }

    public sealed class TunnelClosePayload
    {
        public Guid TunnelId { get; set; }
    }

    public sealed class BindReadyPayload
    {
        public Guid TunnelId { get; set; }
        public string Address { get; set; } = "";
        public int Port { get; set; }

        public IPEndPoint ToEndPoint() => new IPEndPoint(IPAddress.Parse(Address), Port);
    }

    public sealed class BindAcceptedPayload
    {
        public Guid TunnelId { get; set; }
        public string PeerAddress { get; set; } = "";
        public int PeerPort { get; set; }
    }

    public sealed class DataHelloPayload
    {
        public Guid TunnelId { get; set; }
        public Guid SessionId { get; set; }
        /// <summary>HMAC of (SessionId|TunnelId) using auth token; optional.</summary>
        public string? AuthSignature { get; set; }
    }
}
