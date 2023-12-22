namespace TqkLibrary.Proxy.Interfaces
{
    public interface IBaseSource : IDisposable
    {
        Task InitAsync(Uri address, CancellationToken cancellationToken = default);
    }
}
