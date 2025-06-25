using System.Runtime.InteropServices;

namespace TqkLibrary.Proxy.GlobalUnicast.Structs
{
    [StructLayout(LayoutKind.Sequential)]
    struct SCOPE_ID
    {
        public uint Zone;
        public uint Level;
        public uint ScopeId;
        public uint Interface;
    }
}
