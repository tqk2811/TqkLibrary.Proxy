using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Authentications
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseProxyAuthentication
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override abstract bool Equals(object? obj);
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override abstract int GetHashCode();
    }
}
