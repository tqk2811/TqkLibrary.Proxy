namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServer: IDisposable
    {
        void StartListen(bool allowNatTraversal = false);
        void StopListen();
        void ShutdownCurrentConnection();
    }
}
