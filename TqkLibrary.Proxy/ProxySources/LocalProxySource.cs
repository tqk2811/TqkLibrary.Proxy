using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource : IProxySource, IHttpProxy
    {
        public LocalProxySource()
        {

        }
        public LocalProxySource(IPAddress bindIpAddress)
        {
            this.BindIpAddress = bindIpAddress;
        }

        public bool IsSupportUdp { get; set; } = true;
        public bool IsSupportIpv6 { get; set; } = true;
        public bool IsSupportBind { get; set; } = true;
        public IPAddress BindIpAddress { get; set; }

        public Task<IConnectSource> InitConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new ConnectTunnel(this, address, cancellationToken).InitConnectAsync();
        }

        public Task<IBindSource> InitBindAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new BindTunnel(this, cancellationToken).InitBindAsync(address);
        }

        public Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address, CancellationToken cancellationToken = default)
        {
            return new UdpTunnel(this, cancellationToken).InitUdpAsync(address);
        }
    }
}
