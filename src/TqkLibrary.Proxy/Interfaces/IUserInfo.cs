using System.Net;
using TqkLibrary.Proxy.Authentications;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IUserInfo
    {
        Guid TunnelId { get; }
        IPEndPoint IPEndPoint { get; }
        /// <summary>
        /// <see cref="HttpProxyAuthentication"/> for http<br></br>
        /// <see cref="Socks5Authentication"/> for socks5
        /// </summary>
        IAuthentication? Authentication { get; }
    }
}
