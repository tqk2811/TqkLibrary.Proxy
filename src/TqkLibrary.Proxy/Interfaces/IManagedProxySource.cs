namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A way out that holds something up between requests — a subprocess, a session, a handshake —
    /// and can therefore be brought up ahead of the first connection and watched afterwards.
    /// </summary>
    /// <remarks>
    /// Most sources have nothing to hold: a SOCKS5 client dials the upstream per tunnel, so there is
    /// no state that can be "up" or "down" and nothing a host could usefully start early. Those do
    /// not implement this, and testing for it is how a host tells the two apart — the alternative,
    /// which is what consumers ended up writing, is a pattern match on the concrete classes that
    /// silently misses whichever one is added next.
    ///
    /// The contract draws one line, and it is the line that decides who reconnects:
    /// <see cref="IsRunning"/> is "carrying traffic at this instant", which a UI reads, while
    /// <see cref="WaitUntilDownAsync"/> completes only when THIS INSTANCE is finished for good. A
    /// source that mends its own link — an in-process VPN driver with its own backoff — reports the
    /// gap through <see cref="IsRunning"/> and does not complete the wait, because a host tearing
    /// the tunnel down mid-repair would only be racing it. The host's job is the first dial, the
    /// retry once the source has given up, and showing the state; everything in between belongs to
    /// the source.
    ///
    /// A source that never gives up therefore never completes the wait, which is a real shape and
    /// not a hypothetical one: a host that wants to bound it has to decide for itself when
    /// "re-establishing" has gone on too long.
    /// </remarks>
    public interface IManagedProxySource : IProxySource
    {
        /// <summary>True while the way out is up and usable right now.</summary>
        bool IsRunning { get; }

        /// <summary>Where it comes out, for the log line and the status row. Never parsed.</summary>
        string Endpoint { get; }

        /// <summary>
        /// Brings it up, or returns immediately when it is already up. Throws when the configuration
        /// cannot produce a working way out at all.
        /// </summary>
        /// <remarks>
        /// Starting is otherwise lazy, which puts the whole cost — spawning a process, a VPN
        /// handshake, an SSH authentication — on whichever request happens to be first, and again
        /// after every drop. None of that shows up as an error; it just makes one request
        /// inexplicably slow.
        /// </remarks>
        Task StartAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Completes when this instance is beyond recovering by itself, with a human-readable
        /// reason. It is the host's cue to release this source and build a replacement — not a
        /// report of a transient drop.
        /// </summary>
        Task<string> WaitUntilDownAsync(CancellationToken cancellationToken = default);
    }
}
