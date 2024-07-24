using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    public class Socks5_Greeting : IPacketData
    {
        public Socks5_Greeting(IEnumerable<Socks5_Auth> socks5_Auths)
        {
            if (socks5_Auths is null) throw new ArgumentNullException(nameof(socks5_Auths));
            Auths = socks5_Auths.ToArray();
            if (AuthCount == 0) throw new InvalidDataException($"{nameof(socks5_Auths)} is empty");
        }

        private Socks5_Greeting()
        {
        }

        public byte VER { get; private set; } = 0x05;
        public int AuthCount { get { return Auths.Count(); } }
        public IEnumerable<Socks5_Auth> Auths { get; private set; } = Enumerable.Empty<Socks5_Auth>();


        public static async Task<Socks5_Greeting> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_Greeting socks5_Greeting = new Socks5_Greeting();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_Greeting.VER = buffer[0];

            buffer = await stream.ReadBytesAsync(buffer[1], cancellationToken);
            socks5_Greeting.Auths = buffer.Select(x => (Socks5_Auth)x).ToArray();
            return socks5_Greeting;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)AuthCount;
            foreach (byte b in Auths)
            {
                yield return b;
            }
        }
    }
    public static class Socks5_Greeting_Extensions
    {
        public static Task<Socks5_Greeting> Read_Socks5_Greeting_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_Greeting.ReadAsync(stream, cancellationToken);
    }
}
