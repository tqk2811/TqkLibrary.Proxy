using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Enums
{
    internal enum Socks5_STATUS : byte
    {
        RequestGranted = 0x00,
        GeneralFailure = 0x01,
        ConnectionNotAllowedByRuleset = 0x02,
        NetworkUnreachable = 0x03,
        HostUnreachable = 0x04,
        ConnectionRefusedByDestinationHost = 0x05,
        TTL_expired = 0x06,
        CommandNotSupportedOrProtocolError = 0x07,
        AddressTypeNotSupported = 0x08,
    }
}
