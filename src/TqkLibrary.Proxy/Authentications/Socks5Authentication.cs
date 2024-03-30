using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Authentications
{
    public class Socks5Authentication : IAuthentication
    {
        public Socks5Authentication(IEnumerable<Socks5_Auth> authsMethod)
        {
            if (authsMethod is null || !authsMethod.Any())
                throw new ArgumentNullException(nameof(authsMethod));

            Auths = new List<Socks5_Auth>(authsMethod);
        }

        public IReadOnlyList<Socks5_Auth> Auths { get; private set; }
        public Socks5_Auth Choice { get; set; } = Socks5_Auth.Reject;
    }
}
