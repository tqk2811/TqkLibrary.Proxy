using System.Runtime.InteropServices;

namespace TqkLibrary.Proxy.GlobalUnicast.Structs
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MIB_UNICASTIPADDRESS_ROW
    {
        public SOCKADDR_INET Address;
        public ulong InterfaceLuid;             // NET_LUID (64-bit)
        public uint InterfaceIndex;             // NET_IFINDEX
        public uint PrefixOrigin;               // NL_PREFIX_ORIGIN
        public uint SuffixOrigin;               // NL_SUFFIX_ORIGIN
        public uint ValidLifetime;
        public uint PreferredLifetime;
        public byte OnLinkPrefixLength;
        [MarshalAs(UnmanagedType.U1)]
        public bool SkipAsSource;
        public uint DadState;                   // NL_DAD_STATE
        public SCOPE_ID ScopeId;                // 4 x uint32
        public long CreationTimeStamp;          // LARGE_INTEGER
    }
}
