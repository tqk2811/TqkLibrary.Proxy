using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Enums
{
    public enum Socks5_Auth : byte
    {
        NoAuthentication = 0x00,
        /// <summary>
        /// <see href="https://www.rfc-editor.org/rfc/rfc1961"/>
        /// </summary>
        GSSAPI = 0x01,
        /// <summary>
        /// <see href="https://www.rfc-editor.org/rfc/rfc1929"/>
        /// </summary>
        UsernamePassword = 0x02,
        ChallengeHandshakeAuthenticationProtocol = 0x03,
        Unassigned = 0x04,
        ChallengeResponseAuthenticationMethod = 0x05,
        SecureSocketsLayer = 0x06,
        NDSAuthentication = 0x07,
        MultiAuthenticationFramework = 0x08,
        JSONParameterBlock = 0x09,


        Reject = 0xff,
    }
}
