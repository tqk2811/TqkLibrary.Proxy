namespace TqkLibrary.Proxy.Interfaces
{
    public interface IProxyServerHandler
    {
        Task<bool> IsAcceptUserAsync(IUserInfo userInfo, CancellationToken cancellationToken = default);
        Task<bool> IsAcceptDomainAsync(Uri uri, IUserInfo userInfo, CancellationToken cancellationToken = default);
        Task<IProxySource> GetProxySourceAsync(Uri? uri, IUserInfo userInfo, CancellationToken cancellationToken = default);
        /// <summary>
        /// Speed Limit or block connection for user
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="userInfo"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<Stream> StreamHandlerAsync(Stream stream, IUserInfo userInfo, CancellationToken cancellationToken = default);
    }
}
