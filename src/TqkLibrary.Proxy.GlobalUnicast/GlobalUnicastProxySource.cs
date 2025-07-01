using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TqkLibrary.Proxy.GlobalUnicast.Structs;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.GlobalUnicast
{
    public partial class GlobalUnicastProxySource : IProxySource, IDisposable
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        public IPAddress? AssignAddress { get; private set; }
        /// <summary>
        /// zero mean infinity
        /// </summary>
        public TimeSpan LifeTime { get; set; } = TimeSpan.Zero;


        public bool IsSupportUdp => false;

        public bool IsSupportIpv6 => true;

        public bool IsSupportBind => false;

        public GlobalUnicastProxySource()
        {

        }
        ~GlobalUnicastProxySource()
        {
            Dispose(false);
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

        public Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
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
