namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A way out: a factory of tunnels, not a tunnel. One instance is meant to be shared by every
    /// connection that leaves the same way, because an implementation may hold a session behind it
    /// (an SSH channel, a WireGuard handshake, a subprocess) that must not be rebuilt per request.
    /// </summary>
    /// <remarks>
    /// Opening an outgoing connection is the only thing every way out can do, so it is the only
    /// thing here. What some of them can also do is asked for separately —
    /// <see cref="IUdpCapable"/>, <see cref="IBindCapable"/>, <see cref="IAddressFamilyPolicy"/>,
    /// <see cref="IManagedProxySource"/> — and a server tests for the one it needs. This interface
    /// used to ask everything of everyone: three capability flags and three factory methods, of
    /// which most sources answered false and threw. The cost was not the boilerplate. It was that
    /// "supported" and "implemented" drifted apart in both directions — a flag whose setter did
    /// nothing, a source advertising UDP whose tunnel threw NotImplementedException — and nothing
    /// about either showed up at compile time.
    ///
    /// Ownership: whoever built the instance releases it, and nobody else. A server handed a source
    /// by <see cref="IProxyServerHandler.GetProxySourceAsync"/> is a borrower — it must not dispose
    /// what it did not create, or the next connection through the same way out finds it gone.
    ///
    /// Disposal is async because that is what the stateful implementations actually need: closing a
    /// session means a network round trip, and waiting for a subprocess to exit means waiting.
    /// Sources with nothing to release return a completed task. It sits on the interface rather than
    /// on the implementations that happen to need it so that the owner has a single call that always
    /// does the right thing: the earlier arrangement — the caller testing for
    /// <see cref="IDisposable"/> — silently leaked any source that only offered the async form, and
    /// nothing about that showed up at compile time either.
    /// </remarks>
    public interface IProxySource : IAsyncDisposable
    {
        /// <summary>
        /// Opens one outgoing connection through this way out. The tunnel belongs to the caller.
        /// </summary>
        Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default);
    }
}
