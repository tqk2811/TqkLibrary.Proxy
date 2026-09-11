using TqkLibrary.Proxy.SshNet.Interfaces;

namespace TqkLibrary.Proxy.SshNet
{
    public class SshNetConnectionOptions
    {
        public string Host { get; }
        public int Port { get; }
        public string User { get; }

        /// <summary>
        /// Password authentication. If both <see cref="Password"/> and <see cref="IdentityFile"/>
        /// are set, both methods are offered to the server and SSH.NET will try them in order.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Path to a private key file (OpenSSH or PuTTY format supported by SSH.NET).
        /// </summary>
        public string? IdentityFile { get; set; }

        /// <summary>
        /// Passphrase for <see cref="IdentityFile"/>. Ignored when IdentityFile is null.
        /// </summary>
        public string? IdentityFilePassphrase { get; set; }

        /// <summary>
        /// Raw private key bytes (alternative to <see cref="IdentityFile"/>).
        /// </summary>
        public byte[]? IdentityKeyBytes { get; set; }

        /// <summary>
        /// Connection establishment timeout. Default 15 seconds.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// Keep-alive interval. <see cref="TimeSpan.Zero"/> disables. Default 30 seconds.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Optional SHA-256 host key fingerprints (Base64 of the raw 32-byte digest; a "SHA256:"
        /// prefix and padding are tolerated). When non-empty, the server's host key must match one of
        /// them or the session is refused, and <see cref="HostKeyVerifier"/> is not asked.
        /// </summary>
        public IList<string> HostKeyFingerprintsSha256 { get; } = new List<string>();

        /// <summary>
        /// Decides about the server's host key when no fingerprint is pinned — typically a
        /// known_hosts or trust-on-first-use store owned by the application.
        /// </summary>
        public ISshHostKeyVerifier? HostKeyVerifier { get; set; }

        /// <summary>
        /// Accept whatever host key the server presents when neither a fingerprint nor a verifier is
        /// configured. Off by default: an unchecked key lets anyone in the middle impersonate the
        /// server and collect the password. Meant for a lab, never for a server across the internet.
        /// </summary>
        public bool AcceptAnyHostKey { get; set; }

        /// <summary>
        /// Timeout (ms) waiting for the per-target forwarded port to come up before
        /// <c>ConnectAsync</c> is considered failed.
        /// </summary>
        public int ConnectProbeTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Loopback bind host used for the per-target local forwarder. Default 127.0.0.1. Only the
        /// connection this library makes to it is forwarded; any other process that finds the port
        /// is refused before a channel is opened.
        /// </summary>
        public string LocalBindHost { get; set; } = "127.0.0.1";

        public SshNetConnectionOptions(string host, string user, int port = 22)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host required", nameof(host));
            if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("user required", nameof(user));
            if (port <= 0 || port > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(port));
            Host = host;
            User = user;
            Port = port;
        }
    }
}
