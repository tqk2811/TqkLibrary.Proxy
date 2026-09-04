namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    /// <summary>
    /// Says that the wireproxy subprocess is gone, and why.
    /// </summary>
    /// <remarks>
    /// Raised as soon as the process exits rather than at the next connection attempt, so a host
    /// that wants the tunnel permanently up can rebuild it while nothing is waiting on it. A host
    /// that does not subscribe loses nothing: <see cref="WireGuardOptions.AutoRestart"/> still
    /// respawns on the next call.
    /// </remarks>
    public class WireProxyExitedEventArgs : EventArgs
    {
        public WireProxyExitedEventArgs(int exitCode, string standardError)
        {
            ExitCode = exitCode;
            StandardError = standardError;
        }

        public int ExitCode { get; }

        /// <summary>
        /// What wireproxy printed before it died — the only place the reason is ever written.
        /// </summary>
        public string StandardError { get; }
    }
}
