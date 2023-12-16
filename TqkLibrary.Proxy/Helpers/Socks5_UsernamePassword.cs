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
    /// <see href="https://www.rfc-editor.org/rfc/rfc1929"/>
    /// </summary>
    internal class Socks5_UsernamePassword
    {
        internal Socks5_UsernamePassword(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
            if (Encoding.ASCII.GetBytes(userName).Length > 255) throw new InvalidDataException($"{nameof(userName)} too long");
            if (Encoding.ASCII.GetBytes(password).Length > 255) throw new InvalidDataException($"{nameof(password)} too long");
            this.UserName = userName;
            this.Password = password;
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        private Socks5_UsernamePassword()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
        }

        internal byte VER { get; private set; } = 0x01;
        internal string UserName { get; private set; }
        internal string Password { get; private set; }


        internal static async Task<Socks5_UsernamePassword> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
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


        internal byte[] GetByteArray() => GetBytes().ToArray();
        internal IEnumerable<byte> GetBytes()
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
    internal static class Socks5_UsernamePassword_Extensions
    {
        internal static Task<Socks5_UsernamePassword> Read_Socks5_UsernamePassword_Async(this Stream stream, CancellationToken cancellationToken = default)
            => Socks5_UsernamePassword.ReadAsync(stream, cancellationToken);
    }
}
