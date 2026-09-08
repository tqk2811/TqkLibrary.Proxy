namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A way out that resolves names itself, and can therefore be told to stop offering IPv6.
    /// </summary>
    /// <remarks>
    /// Only a way out that does its own lookup can honour this. Through a proxy the destination is
    /// handed over as a name and the upstream resolves it, so there is nothing on this side to
    /// filter — which is why the old <c>IsSupportIpv6</c> on every source was a settable property
    /// that did nothing on all but two of them, and why a host that set it believed it had turned
    /// something off. A host that wants IPv6 kept off a proxied route has to refuse the connection
    /// itself.
    ///
    /// It is a policy, not a fact: setting it does not conjure an IPv6 route, and a way out that
    /// never obtained one stays without it however this is set.
    /// </remarks>
    public interface IAddressFamilyPolicy
    {
        /// <summary>
        /// False to make name lookups return A records only, so the connection is never handed an
        /// AAAA it has no route for.
        /// </summary>
        bool AllowIpv6 { get; set; }
    }
}
