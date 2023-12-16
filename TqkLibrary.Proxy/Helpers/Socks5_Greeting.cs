using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    internal class Socks5_Greeting
    {
        internal Socks5_Greeting(IEnumerable<Socks5_Auth> socks5_Auths)
        {
            if (socks5_Auths is null) throw new ArgumentNullException(nameof(socks5_Auths));
            this.Auths = socks5_Auths.ToArray();
            if (this.AuthCount == 0) throw new InvalidDataException($"{nameof(socks5_Auths)} is empty");
        }

        private Socks5_Greeting()
        {
        }

        internal byte VER { get; private set; } = 0x05;
        internal int AuthCount { get { return Auths.Count(); } }
        internal IEnumerable<Socks5_Auth> Auths { get; private set; } = Enumerable.Empty<Socks5_Auth>();


        internal static async Task<Socks5_Greeting> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_Greeting socks5_Greeting = new Socks5_Greeting();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_Greeting.VER = buffer[0];

            buffer = await stream.ReadBytesAsync(buffer[1], cancellationToken);
            socks5_Greeting.Auths = buffer.Select(x => (Socks5_Auth)x).ToArray();
            return socks5_Greeting;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)AuthCount;
            foreach (byte b in Auths)
            {
                yield return b;
            }
        }
    }
    internal static class Socks5_Greeting_Extensions
    {
        internal static Task<Socks5_Greeting> Read_Socks5_Greeting_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_Greeting.ReadAsync(stream, cancellationToken);
    }
}
