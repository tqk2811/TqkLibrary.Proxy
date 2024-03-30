namespace TqkLibrary.Proxy.Interfaces
{
    public interface IConnectSource : IBaseSource
    {
        Task ConnectAsync(Uri address, CancellationToken cancellationToken = default);
    }
}
