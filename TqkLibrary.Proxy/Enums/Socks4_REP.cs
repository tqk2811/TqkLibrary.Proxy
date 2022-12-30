namespace TqkLibrary.Proxy.ProxySources
{
    internal enum Socks4_REP : byte
    {
        RequestGranted = 0x5a,
        RequestRejectedOrFailed = 0x5b,
        RequestFailedBecauseClientIsNotRunningIdentd = 0x5c,
        RequestFailedBecauseClientIdentdCouldNotConfirmTheUserIdInTheRequest = 0x5d
    }
}
