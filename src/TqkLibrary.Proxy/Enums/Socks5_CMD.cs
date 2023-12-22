using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Enums
{
    internal enum Socks5_CMD : byte
    {
        EstablishStreamConnection = 0x01,
        EstablishPortBinding = 0x02,
        AssociateUDP = 0x03
    }
}
