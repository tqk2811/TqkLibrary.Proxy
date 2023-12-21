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
    internal class Socks5_GreetingResponse
    {
        internal Socks5_GreetingResponse(Socks5_Auth socks5_Auth)
        {
            this.CAUTH = socks5_Auth;
        }

        private Socks5_GreetingResponse()
        {
        }

        internal byte VER { get; private set; } = 0x05;
        internal Socks5_Auth CAUTH { get; private set; }


        internal static async Task<Socks5_GreetingResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_GreetingResponse socks5_GreetingResponse = new Socks5_GreetingResponse();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_GreetingResponse.VER = buffer[0];
            socks5_GreetingResponse.CAUTH = (Socks5_Auth)buffer[1];
            return socks5_GreetingResponse;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)CAUTH;
        }
    }
    internal static class Socks5_GreetingResponse_Extensions
    {
        internal static Task<Socks5_GreetingResponse> Read_Socks5_GreetingResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_GreetingResponse.ReadAsync(stream, cancellationToken);
    }
}
