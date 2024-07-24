using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1928"/>
    /// </summary>
    public class Socks5_GreetingResponse : IPacketData
    {
        public Socks5_GreetingResponse(Socks5_Auth socks5_Auth)
        {
            CAUTH = socks5_Auth;
        }

        private Socks5_GreetingResponse()
        {
        }

        public byte VER { get; private set; } = 0x05;
        public Socks5_Auth CAUTH { get; private set; }


        public static async Task<Socks5_GreetingResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_GreetingResponse socks5_GreetingResponse = new Socks5_GreetingResponse();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_GreetingResponse.VER = buffer[0];
            socks5_GreetingResponse.CAUTH = (Socks5_Auth)buffer[1];
            return socks5_GreetingResponse;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return (byte)CAUTH;
        }
    }
    public static class Socks5_GreetingResponse_Extensions
    {
        public static Task<Socks5_GreetingResponse> Read_Socks5_GreetingResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_GreetingResponse.ReadAsync(stream, cancellationToken);
    }
}
