namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServer
    {
        void StartListen();
        void StopListen();
        void ShutdownCurrentConnection();
    }
}
