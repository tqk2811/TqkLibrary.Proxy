using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Authentications
{
    /// <summary>
    /// Carries the UserId (ID field) supplied by a SOCKS4/4a client in its CONNECT/BIND request.
    /// Inspect <see cref="UserId"/> inside <see cref="IProxyServerHandler.IsAcceptUserAsync"/>
    /// to allow or reject the connection.
    /// </summary>
    public class Socks4Authentication : IAuthentication
    {
        public Socks4Authentication(string userId)
        {
            UserId = userId ?? string.Empty;
        }

        public string UserId { get; }
    }
}
