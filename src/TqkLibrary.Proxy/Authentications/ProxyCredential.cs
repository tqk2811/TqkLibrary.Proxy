using System.Net;

namespace TqkLibrary.Proxy.Authentications
{
    public class ProxyCredential : BaseProxyAuthentication
    {
        public ProxyCredential(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
            UserName = userName;
            Password = password;
        }

        public string UserName { get; }
        public string Password { get; set; }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is ProxyCredential proxyCredential)
            {
                return GetHashCode() == proxyCredential.GetHashCode();
            }

            return false;
        }

        public static explicit operator NetworkCredential(ProxyCredential proxyCredential)
            => new NetworkCredential(proxyCredential.UserName, proxyCredential.Password);

        public static implicit operator ProxyCredential(NetworkCredential networkCredential)
            => new ProxyCredential(networkCredential.UserName, networkCredential.Password);

        public override int GetHashCode()
        {
            return $"{UserName}|{Password}".GetHashCode();
        }
    }
}
