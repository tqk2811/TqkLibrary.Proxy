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
        /// Optional SHA-256 host key fingerprints (Base64 of raw 32-byte digest, no "SHA256:" prefix).
        /// When non-empty, the server host key must match one of the listed values or the
        /// connection is aborted. When empty, the host key is accepted on first use.
        /// </summary>
        public IList<string> HostKeyFingerprintsSha256 { get; } = new List<string>();

        /// <summary>
        /// Timeout (ms) waiting for the per-target forwarded port to come up before
        /// <c>ConnectAsync</c> is considered failed.
        /// </summary>
        public int ConnectProbeTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Loopback bind host used for the per-target local forwarder. Default 127.0.0.1.
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
