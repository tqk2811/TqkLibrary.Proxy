using System.Runtime.InteropServices;

namespace TqkLibrary.Proxy.GlobalUnicast.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    struct SOCKADDR_INET
    {
        public ushort si_family; // AF_INET6 = 23

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
        public byte[] si_data;
    }
}
