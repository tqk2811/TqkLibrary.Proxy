using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.GlobalUnicast.Structs;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.GlobalUnicast
{
    public partial class GlobalUnicastProxySource : IProxySource, IDisposable
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly ILoggerFactory? _loggerFactory;
        public IPAddress? AssignAddress { get; private set; }
        /// <summary>
        /// zero mean infinity
        /// </summary>
        public TimeSpan LifeTime { get; set; } = TimeSpan.Zero;


        // Neither IUdpCapable nor IBindCapable, and no address family setting: this source exists to
        // pick which of the machine's global unicast IPv6 addresses a connection leaves from, so
        // IPv6 is not something it could be told to leave out.

        public GlobalUnicastProxySource(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
        }
        ~GlobalUnicastProxySource()
        {
            Dispose(false);
        }
        /// <remarks>
        /// The work itself is still synchronous — releasing the bound addresses is a local operation — so this hands back a completed
        /// task rather than pretending otherwise. It exists because the owner releases a way out
        /// through <see cref="IProxySource"/> and should not have to know which shape of disposal a
        /// particular source happens to offer.
        /// </remarks>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return default;
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        void Dispose(bool disposing)
        {
            UnInit();
            if (disposing)
            {
                _semaphore.Dispose();
            }
        }

        public async Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            await InitAsync();
            return new ConnectTunnel(this, tunnelId);
        }

        async Task InitAsync()
        {
            if (AssignAddress is not null)
                return;

            await _semaphore.WaitAsync();
            try
            {
                if (AssignAddress is not null)
                    return;
                IPAddress? prefix = SlaccHelper.FindGlobalUnicastPrefix();
                if (prefix is null)
                    throw new InvalidOperationException();

                IPAddress iPAddress = SlaccHelper.GenerateSlaacAddress(prefix);
                await SlaccHelper.AssignIPv6ToFirstUpInterfaceAsync(iPAddress, LifeTime);
                AssignAddress = iPAddress;
            }
            finally
            {
                _semaphore.Release();
            }
        }
        void UnInit()
        {
            _semaphore.Wait();
            try
            {
                if (AssignAddress is not null)
                {
                    if (SlaccHelper.RemoveIPv6FromFirstUpInterface(AssignAddress))
                    {
                        AssignAddress = null;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        async Task UnInitAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                if (AssignAddress is not null)
                {
                    if (SlaccHelper.RemoveIPv6FromFirstUpInterface(AssignAddress))
                    {
                        AssignAddress = null;
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }


    }
}
