namespace TqkLibrary.Proxy.Reverse.Protocol
{
    /// <summary>
    /// Wire frame: [len:4 BE][type:1][payload[len-1]]
    /// Payload is UTF-8 JSON for control frames.
    /// </summary>
    public sealed class ReverseFrame
    {
        public FrameType Type { get; }
        public byte[] Payload { get; }

        public ReverseFrame(FrameType type, byte[] payload)
        {
            Type = type;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public static ReverseFrame Empty(FrameType type) => new ReverseFrame(type, Array.Empty<byte>());
    }
}
