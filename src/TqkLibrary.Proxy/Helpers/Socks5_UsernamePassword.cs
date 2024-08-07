﻿using System.Text;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.Helpers
{
    /// <summary>
    /// <see href="https://www.rfc-editor.org/rfc/rfc1929"/>
    /// </summary>
    public class Socks5_UsernamePassword: IPacketData
    {
        public Socks5_UsernamePassword(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
            if (Encoding.ASCII.GetBytes(userName).Length > 255) throw new InvalidDataException($"{nameof(userName)} too long");
            if (Encoding.ASCII.GetBytes(password).Length > 255) throw new InvalidDataException($"{nameof(password)} too long");
            UserName = userName;
            Password = password;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Socks5_UsernamePassword()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        public byte VER { get; private set; } = 0x01;
        public string UserName { get; private set; }
        public string Password { get; private set; }


        public static async Task<Socks5_UsernamePassword> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            Socks5_UsernamePassword socks5_UsernamePassword = new Socks5_UsernamePassword();
            byte[] buffer = await stream.ReadBytesAsync(2, cancellationToken);
            socks5_UsernamePassword.VER = buffer[0];

            buffer = await stream.ReadBytesAsync(buffer[1], cancellationToken);
            socks5_UsernamePassword.UserName = Encoding.ASCII.GetString(buffer);

            buffer = await stream.ReadBytesAsync(1, cancellationToken);
            buffer = await stream.ReadBytesAsync(buffer[0], cancellationToken);
            socks5_UsernamePassword.Password = Encoding.ASCII.GetString(buffer);

            return socks5_UsernamePassword;
        }


        public IEnumerable<byte> GetBytes()
        {
            yield return VER;

            var userNameBuffer = Encoding.ASCII.GetBytes(UserName);
            yield return (byte)userNameBuffer.Length;
            foreach (byte b in userNameBuffer)
            {
                yield return b;
            }

            var passwordBuffer = Encoding.ASCII.GetBytes(Password);
            yield return (byte)passwordBuffer.Length;
            foreach (byte b in passwordBuffer)
            {
                yield return b;
            }
        }
    }
    public static class Socks5_UsernamePassword_Extensions
    {
        public static Task<Socks5_UsernamePassword> Read_Socks5_UsernamePassword_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_UsernamePassword.ReadAsync(stream, cancellationToken);
    }
}
