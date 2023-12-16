using System.Net;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IBindSource : IBaseSource
    {
        Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default);
        Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default);
    }
}
