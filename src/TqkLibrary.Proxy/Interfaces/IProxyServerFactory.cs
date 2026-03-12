using TqkLibrary.Proxy.StreamHelpers;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServerFactory
    {
        Task<IProxyServer> CreateAsync(PreReadStream preReadStream, CancellationToken cancellationToken = default);
    }
}
