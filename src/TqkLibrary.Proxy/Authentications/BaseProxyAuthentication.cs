using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.Authentications
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseProxyAuthentication : IAuthentication
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
