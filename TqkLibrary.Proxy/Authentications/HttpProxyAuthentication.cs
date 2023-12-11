using System.Net;

namespace TqkLibrary.Proxy.Authentications
{
    public class HttpProxyAuthentication : BaseProxyAuthentication
    {
        public HttpProxyAuthentication(string userName, string password)
        {
            if (string.IsNullOrWhiteSpace(userName)) throw new ArgumentNullException(nameof(userName));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentNullException(nameof(password));
            this.UserName = userName;
            this.Password = password;
        }

        public string UserName { get; }
        public string Password { get; set; }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is HttpProxyAuthentication httpProxyAuthentication)
            {
                return this.GetHashCode() == httpProxyAuthentication.GetHashCode();
            }

            return false;
        }

        public static explicit operator NetworkCredential(HttpProxyAuthentication httpProxyAuthentication)
            => new NetworkCredential(httpProxyAuthentication.UserName, httpProxyAuthentication.Password);

        public static implicit operator HttpProxyAuthentication(NetworkCredential networkCredential)
            => new HttpProxyAuthentication(networkCredential.UserName, networkCredential.Password);

        public override int GetHashCode()
        {
            return $"{UserName}|{Password}".GetHashCode();
        }
    }
}
