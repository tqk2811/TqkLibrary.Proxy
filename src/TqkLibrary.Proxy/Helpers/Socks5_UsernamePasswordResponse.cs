﻿using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1929"/>
    /// </summary>
    public class Socks5_UsernamePasswordResponse : IPacketData
    {
        public Socks5_UsernamePasswordResponse(byte status)
        {
            STATUS = status;
        }

        private Socks5_UsernamePasswordResponse()
        {
        }

        public byte VER { get; private set; } = 0x01;
        public byte STATUS { get; private set; }


        public static async Task<Socks5_UsernamePasswordResponse> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_UsernamePasswordResponse socks5_UsernamePasswordResponse = new Socks5_UsernamePasswordResponse();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_UsernamePasswordResponse.VER = buffer[0];
            socks5_UsernamePasswordResponse.STATUS = buffer[1];
            return socks5_UsernamePasswordResponse;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return VER;
            yield return STATUS;
        }
    }
    public static class Socks5_UsernamePasswordResponse_Extensions
    {
        public static Task<Socks5_UsernamePasswordResponse> Read_Socks5_UsernamePasswordResponse_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_UsernamePasswordResponse.ReadAsync(stream, cancellationToken);
    }
}
