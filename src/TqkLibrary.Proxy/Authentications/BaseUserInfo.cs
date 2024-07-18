using System.Net;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Authentications
{
    public class BaseUserInfo : IUserInfo
    {
        public BaseUserInfo(IPEndPoint iPEndPoint, Guid tunnelId)
        {
            IPEndPoint = iPEndPoint;
            TunnelId = tunnelId;
        }
        public Guid TunnelId { get; }
        public IPEndPoint IPEndPoint { get; }

        public virtual IAuthentication? Authentication { get; set; }

    }
}
