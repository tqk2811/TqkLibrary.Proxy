using Microsoft.Extensions.Logging;
using Renci.SshNet.Common;
using TqkLibrary.Proxy.SshNet.Models;

namespace TqkLibrary.Proxy.SshNet
{
    /// <summary>
    /// The host key check for one connection attempt. One is made per <c>SshClient</c> so that a
    /// refusal can be read back afterwards: SSH.NET reports a refused key as a generic key exchange
    /// failure, and the reason — which fingerprint, refused by whom — would otherwise be lost.
    /// </summary>
    /// <remarks>
    /// The order is: a pinned fingerprint list when there is one, else the verifier, else
    /// <see cref="SshNetConnectionOptions.AcceptAnyHostKey"/>. The handler is always attached. It used
    /// to be attached only when fingerprints were pinned, which left every other session accepting
    /// any key at all — while the option's documentation promised "accepted on first use".
    /// </remarks>
    internal sealed class SshNetHostKeyGate
    {
        private readonly SshNetConnectionOptions _options;
        private readonly ILogger? _logger;

        public SshNetHostKeyGate(SshNetConnectionOptions options, ILogger? logger)
        {
            _options = options;
            _logger = logger;
        }

        /// <summary>The key this attempt refused, or null when none was refused.</summary>
        public SshHostKey? RejectedKey { get; private set; }

        public string? RejectedReason { get; private set; }

        public void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
        {
            var key = new SshHostKey(
                _options.Host, _options.Port, e.HostKeyName, e.FingerPrintSHA256, e.HostKey);

            SshHostKeyVerdict verdict = Decide(key);
            e.CanTrust = verdict.IsTrusted;
            if (verdict.IsTrusted) return;

            RejectedKey = key;
            RejectedReason = verdict.Reason;
            _logger?.LogWarning("Refusing SSH host key {HostKey}: {Reason}", key, verdict.Reason);
        }

        private SshHostKeyVerdict Decide(SshHostKey key)
        {
            if (_options.HostKeyFingerprintsSha256.Count > 0)
            {
                bool pinned = _options.HostKeyFingerprintsSha256
                    .Select(SshHostKey.NormalizeFingerprint)
                    .Contains(key.FingerprintSha256, StringComparer.Ordinal);
                return pinned
                    ? SshHostKeyVerdict.Trust()
                    : SshHostKeyVerdict.Reject("it is not one of the pinned fingerprints");
            }

            if (_options.HostKeyVerifier != null)
            {
                // A verifier that throws must not become a trusted key, and an exception thrown out
                // of this event would surface as the same unexplained key exchange failure.
                try { return _options.HostKeyVerifier.Verify(key); }
                catch (Exception ex) { return SshHostKeyVerdict.Reject($"the host key store failed: {ex.Message}"); }
            }

            return _options.AcceptAnyHostKey
                ? SshHostKeyVerdict.Trust()
                : SshHostKeyVerdict.Reject(
                    "no fingerprint is pinned and no host key verifier is configured");
        }
    }
}
