namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServer: IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="allowNatTraversal">Window Only</param>
        void StartListen(bool allowNatTraversal = false);
        void StopListen();
        void ShutdownCurrentConnection();
    }
}
