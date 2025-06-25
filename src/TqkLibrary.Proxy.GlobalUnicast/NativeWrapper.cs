using System.Runtime.InteropServices;
using TqkLibrary.Proxy.GlobalUnicast.Structs;

namespace TqkLibrary.Proxy.GlobalUnicast
{
    internal static class NativeWrapper
    {
        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern int CreateUnicastIpAddressEntry(ref MIB_UNICASTIPADDRESS_ROW Row);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern int InitializeUnicastIpAddressEntry(out MIB_UNICASTIPADDRESS_ROW Row);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        public static extern int DeleteUnicastIpAddressEntry(ref MIB_UNICASTIPADDRESS_ROW Row);
    }
}
