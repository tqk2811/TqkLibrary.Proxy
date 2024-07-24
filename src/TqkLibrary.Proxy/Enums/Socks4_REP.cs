namespace TqkLibrary.Proxy.Enums
{
    public enum Socks4_REP : byte
    {
        /// <summary>
        /// Request granted
        /// </summary>
        RequestGranted = 0x5a,
        /// <summary>
        /// Request rejected or failed
        /// </summary>
        RequestRejectedOrFailed = 0x5b,
        /// <summary>
        /// Request Failed Because Client Is Not Running Identd
        /// </summary>
        ClientIsNotRunningIdentd = 0x5c,
        /// <summary>
        /// Request Failed Because Client Identd Could Not Confirm The User Id In The Request
        /// </summary>
        CouldNotConfirmTheUserId = 0x5d
    }
}
