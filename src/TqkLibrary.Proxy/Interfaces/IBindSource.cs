using System.Net;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IBindSource : IBaseSource
    {
        Task<IPEndPoint> BindAsync(CancellationToken cancellationToken = default);
    }
}
