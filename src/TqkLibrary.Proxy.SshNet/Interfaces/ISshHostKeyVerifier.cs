using TqkLibrary.Proxy.SshNet.Models;

namespace TqkLibrary.Proxy.SshNet.Interfaces
{
    /// <summary>
    /// Decides whether the key a server presents is the one this machine expects for it — a
    /// known_hosts file, a trust-on-first-use store, a pinned list.
    /// </summary>
    /// <remarks>
    /// Called synchronously in the middle of the key exchange, once per session rather than once per
    /// tunnel, so a store that reads or writes a small file here is fine; anything that could wait on
    /// the network or a person is not.
    /// </remarks>
    public interface ISshHostKeyVerifier
    {
        SshHostKeyVerdict Verify(SshHostKey hostKey);
    }
}
