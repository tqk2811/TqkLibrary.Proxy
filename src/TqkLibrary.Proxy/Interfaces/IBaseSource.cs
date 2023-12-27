namespace TqkLibrary.Proxy.Interfaces
{
    public interface IBaseSource : IDisposable
    {
        Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default);
    }
}
