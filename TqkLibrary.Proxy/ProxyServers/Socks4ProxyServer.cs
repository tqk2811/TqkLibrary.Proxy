using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks4ProxyServer : BaseProxyServer, ISocks4Proxy
    {
        public Socks4ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource) : base(iPEndPoint, proxySource)
        {

        }
        public bool IsUseSocks4A { get; set; } = true;

        protected override Task ProxyWorkAsync(Stream client_stream, EndPoint client_EndPoint, CancellationToken cancellationToken = default)
        {
            return new Socks4ProxyServerTunnel(this, client_stream, client_EndPoint, cancellationToken).ProxyWorkAsync();
        }
    }
}
