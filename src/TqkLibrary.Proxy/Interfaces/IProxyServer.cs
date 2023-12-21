namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServer: IDisposable
    {
        void StartListen();
        void StopListen();
        void ShutdownCurrentConnection();
    }
}
