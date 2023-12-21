using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1929"/>
    /// </summary>
    internal class Socks5_UsernamePasswordResponse
    {
        internal Socks5_UsernamePasswordResponse(byte status)
        {
            this.STATUS = status;
        }

        private Socks5_UsernamePasswordResponse()
        {
        }

        internal byte VER { get; private set; } = 0x01;
        internal byte STATUS { get; private set; }


        internal static async Task<Socks5_UsernamePasswordResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_UsernamePasswordResponse socks5_UsernamePasswordResponse = new Socks5_UsernamePasswordResponse();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_UsernamePasswordResponse.VER = buffer[0];
            socks5_UsernamePasswordResponse.STATUS = buffer[1];
            return socks5_UsernamePasswordResponse;
        }


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return STATUS;
        }
    }
    internal static class Socks5_UsernamePasswordResponse_Extensions
    {
        internal static Task<Socks5_UsernamePasswordResponse> Read_Socks5_UsernamePasswordResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_UsernamePasswordResponse.ReadAsync(stream, cancellationToken);
    }
}
