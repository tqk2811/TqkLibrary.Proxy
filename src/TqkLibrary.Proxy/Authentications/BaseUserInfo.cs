using System.Net;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Authentications
{
    public class BaseUserInfo : IUserInfo
    {
        public BaseUserInfo(IPEndPoint iPEndPoint)
        {
            IPEndPoint = iPEndPoint;
        }
        public IPEndPoint IPEndPoint { get; }

        public virtual IAuthentication? Authentication { get; set; }
    }
}
