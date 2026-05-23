using System.Net;
using TqkLibrary.Proxy.Authentications;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IUserInfo
    {
        Guid TunnelId { get; }
        IPEndPoint IPEndPoint { get; }
        /// <summary>
        /// <see cref="ProxyCredential"/> for http and socks5 username/password auth
        /// </summary>
        IAuthentication? Authentication { get; }
    }
}
