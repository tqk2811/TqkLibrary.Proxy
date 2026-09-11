using TqkLibrary.Proxy.SshNet.Models;

namespace TqkLibrary.Proxy.SshNet.Exceptions
{
    /// <summary>
    /// The session was refused because the server's host key is not the one expected. Kept apart from
    /// every other connect failure because it is the one that must not be retried blindly: it means
    /// either the server's key really changed or the connection is being intercepted.
    /// </summary>
    public class SshNetHostKeyRejectedException : SshNetException
    {
        public SshNetHostKeyRejectedException(SshHostKey hostKey, string reason, Exception? innerException = null)
            : base($"SSH host key for {hostKey?.Host}:{hostKey?.Port} was refused: {reason} "
                   + $"(the server presented {hostKey?.Algorithm} SHA256:{hostKey?.FingerprintSha256})", innerException)
        {
            HostKey = hostKey ?? throw new ArgumentNullException(nameof(hostKey));
            Reason = reason;
        }

        public SshHostKey HostKey { get; }

        public string Reason { get; }
    }
}
