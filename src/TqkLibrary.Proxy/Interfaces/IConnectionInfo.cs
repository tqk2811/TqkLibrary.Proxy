using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IConnectionInfo
    {
        IPEndPoint IPEndPoint { get; }
        IUserInfo UserInfo { get; }
    }
}
