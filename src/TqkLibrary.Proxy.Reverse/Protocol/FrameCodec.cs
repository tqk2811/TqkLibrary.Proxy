using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace TqkLibrary.Proxy.Reverse.Protocol
{
    public static class FrameCodec
    {
        public const int MaxFrameSize = 1024 * 1024; // 1 MiB safety cap

        private static readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
        };

        public static ReverseFrame Encode<T>(FrameType type, T payload)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, _json);
            return new ReverseFrame(type, bytes);
        }

        public static T Decode<T>(ReverseFrame frame)
        {
            if (frame.Payload.Length == 0)
                return Activator.CreateInstance<T>();
            return JsonSerializer.Deserialize<T>(frame.Payload, _json)
                ?? throw new InvalidDataException($"Empty {typeof(T).Name}");
        }

        public static async Task WriteAsync(Stream stream, ReverseFrame frame, CancellationToken ct)
        {
            var len = frame.Payload.Length + 1;
            if (len > MaxFrameSize) throw new InvalidDataException("Frame too large");

            var header = new byte[5];
            BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), len);
            header[4] = (byte)frame.Type;

#if NETSTANDARD2_0
            await stream.WriteAsync(header, 0, header.Length, ct).ConfigureAwait(false);
            if (frame.Payload.Length > 0)
                await stream.WriteAsync(frame.Payload, 0, frame.Payload.Length, ct).ConfigureAwait(false);
#else
            await stream.WriteAsync(header, ct).ConfigureAwait(false);
            if (frame.Payload.Length > 0)
                await stream.WriteAsync(frame.Payload, ct).ConfigureAwait(false);
#endif
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }

        public static async Task<ReverseFrame> ReadAsync(Stream stream, CancellationToken ct)
        {
            var header = new byte[5];
            await ReadExactAsync(stream, header, 0, 5, ct).ConfigureAwait(false);
            var len = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(0, 4));
            if (len < 1 || len > MaxFrameSize) throw new InvalidDataException($"Bad frame length {len}");
            var type = (FrameType)header[4];
            var payloadLen = len - 1;
            var payload = payloadLen == 0 ? Array.Empty<byte>() : new byte[payloadLen];
            if (payloadLen > 0)
                await ReadExactAsync(stream, payload, 0, payloadLen, ct).ConfigureAwait(false);
            return new ReverseFrame(type, payload);
        }

        private static async Task ReadExactAsync(Stream s, byte[] buf, int offset, int count, CancellationToken ct)
        {
            while (count > 0)
            {
#if NETSTANDARD2_0
                var n = await s.ReadAsync(buf, offset, count, ct).ConfigureAwait(false);
#else
                var n = await s.ReadAsync(buf.AsMemory(offset, count), ct).ConfigureAwait(false);
#endif
                if (n <= 0) throw new EndOfStreamException("Peer closed connection");
                offset += n;
                count -= n;
            }
        }
    }
}
