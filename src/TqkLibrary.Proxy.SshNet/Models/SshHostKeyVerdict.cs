namespace TqkLibrary.Proxy.SshNet.Models
{
    /// <summary>
    /// What a verifier decided about a host key. A refusal carries its reason, because the handshake
    /// that fails over it only says "key exchange negotiation failed" — the reason is the one thing
    /// that tells the user whether the server was re-installed or someone is in the middle.
    /// </summary>
    public sealed class SshHostKeyVerdict
    {
        private static readonly SshHostKeyVerdict _trusted = new SshHostKeyVerdict(true, null);

        private SshHostKeyVerdict(bool isTrusted, string? reason)
        {
            IsTrusted = isTrusted;
            Reason = reason;
        }

        public bool IsTrusted { get; }

        /// <summary>Why the key was refused. Null when it was trusted.</summary>
        public string? Reason { get; }

        public static SshHostKeyVerdict Trust() => _trusted;

        public static SshHostKeyVerdict Reject(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("reason required", nameof(reason));
            return new SshHostKeyVerdict(false, reason);
        }
    }
}
