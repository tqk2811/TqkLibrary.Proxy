namespace TqkLibrary.Proxy.SshNet.Models
{
    /// <summary>
    /// The key a server presented during the handshake, together with the address it was presented
    /// for — a key is only ever trusted for one host and port, never on its own.
    /// </summary>
    public sealed class SshHostKey
    {
        private readonly byte[] _keyBlob;

        public SshHostKey(string host, int port, string algorithm, string fingerprintSha256, byte[] keyBlob)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host required", nameof(host));
            if (port <= 0 || port > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(port));
            Host = host;
            Port = port;
            Algorithm = algorithm ?? string.Empty;
            FingerprintSha256 = NormalizeFingerprint(fingerprintSha256);
            _keyBlob = keyBlob ?? throw new ArgumentNullException(nameof(keyBlob));
        }

        public string Host { get; }

        public int Port { get; }

        /// <summary>The key's algorithm as the server named it, e.g. <c>ssh-ed25519</c>.</summary>
        public string Algorithm { get; }

        /// <summary>
        /// SHA-256 of the key blob in Base64, without the <c>SHA256:</c> prefix and without padding —
        /// the same text <c>ssh-keygen -lf</c> prints after the prefix.
        /// </summary>
        public string FingerprintSha256 { get; }

        /// <summary>The key itself in SSH wire format, as a copy.</summary>
        public byte[] GetKeyBlob() => (byte[])_keyBlob.Clone();

        /// <summary>
        /// A fingerprint as people paste it — with or without the <c>SHA256:</c> prefix and the
        /// padding — reduced to the one form two fingerprints can be compared in.
        /// </summary>
        public static string NormalizeFingerprint(string? fingerprint)
        {
            if (fingerprint is null) return string.Empty;
            string value = fingerprint.Trim();
            if (value.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase))
                value = value.Substring("SHA256:".Length);
            return value.TrimEnd('=');
        }

        public override string ToString() => $"{Algorithm} SHA256:{FingerprintSha256} for {Host}:{Port}";
    }
}
