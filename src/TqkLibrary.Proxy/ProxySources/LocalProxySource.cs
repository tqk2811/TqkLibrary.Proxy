using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource : IProxySource, IUdpCapable, IBindCapable, IAddressFamilyPolicy, IHttpProxy
    {
        private readonly ILoggerFactory? _loggerFactory;

        public LocalProxySource(ILoggerFactory? loggerFactory = null)
        {
            _loggerFactory = loggerFactory;
        }

        public virtual bool IsSupportUdp { get; set; } = true;

        /// <summary>
        /// This source resolves names itself, so turning IPv6 off here really does keep AAAA records
        /// out of the answer — for a proxy the upstream resolves and there is nothing on this side
        /// to filter, which is why only this and a tunnel with its own stack offer the setting.
        /// </summary>
        public virtual bool AllowIpv6 { get; set; } = true;
        /// <summary>
        /// use for connect to target host dns -> ip address<br/>
        /// null is default by <see cref="Dns.GetHostAddressesAsync"/><br/>
        /// True for Prioritize Ipv4, false for Prioritize Ipv6 (must enable <see cref="AllowIpv6"/>)
        /// </summary>
        public virtual bool? IsPrioritizeIpv4 { get; set; } = null;
        public virtual bool IsSupportBind { get; set; } = true;

        /// <summary>Nothing to release: this source holds no session, only the address of one.</summary>
        public ValueTask DisposeAsync() => default;
        /// <summary>
        /// window only
        /// </summary>
        public virtual bool IsAllowNatTraversal { get; set; } = false;
        public virtual int BindListenTimeout { get; set; } = 30000;

        public virtual Task<IConnectSource> GetConnectSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IConnectSource>(new ConnectTunnel(this, tunnelId));
        }

        public virtual Task<IBindSource> GetBindSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IBindSource>(new BindTunnel(this, tunnelId));
        }

        public virtual Task<IUdpAssociateSource> GetUdpAssociateSourceAsync(Guid tunnelId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IUdpAssociateSource>(new UdpTunnel(this, tunnelId));
        }

        public virtual Task<IPEndPoint> GetListenEndPointAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IPEndPoint(IPAddress.Any, 0));
        }
        public virtual Task<IPAddress> GetResponseIPAddressAsync(CancellationToken cancellationToken = default)
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint;
                return Task.FromResult(endPoint.Address);
            }
        }
    }
}
