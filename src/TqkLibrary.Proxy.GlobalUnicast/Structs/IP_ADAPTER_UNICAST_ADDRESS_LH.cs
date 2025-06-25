using System.Runtime.InteropServices;

namespace TqkLibrary.Proxy.GlobalUnicast.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    struct IP_ADAPTER_UNICAST_ADDRESS_LH
    {
        public UInt32 Length;
        public UInt32 Flags;
        public IntPtr Next;
        public SOCKADDR_INET Address;
        // ...
    }
}
