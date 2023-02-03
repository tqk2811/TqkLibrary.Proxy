using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource : IProxySource, ISocks5Proxy
    {
        public IPEndPoint IPEndPoint { get; }
        public NetworkCredential NetworkCredential { get; }
        public Socks5ProxySource(IPEndPoint iPEndPoint)
        {
            this.IPEndPoint = iPEndPoint ?? throw new ArgumentNullException(nameof(iPEndPoint));
        }
        public Socks5ProxySource(IPEndPoint iPEndPoint, NetworkCredential networkCredential) : this(iPEndPoint)
        {
            this.NetworkCredential = networkCredential ?? throw new ArgumentNullException(nameof(networkCredential));
        }

        public bool IsSupportUdp => true;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => true;

        public Task<IBindSource> InitBindAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new Socks5ProxySourceTunnel(this, cancellationToken).InitBindAsync(address);
        }

        public Task<IConnectSource> InitConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new Socks5ProxySourceTunnel(this, cancellationToken).InitConnectAsync(address);
        }

        public Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new Socks5ProxySourceTunnel(this, cancellationToken).InitUdpAssociateAsync(address);
        }
    }
}
