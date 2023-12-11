using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHeplers;
using TqkLibrary.Proxy.Filters;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks4ProxyServer : BaseProxyServer, ISocks4Proxy
    {
        public Socks4ProxyServerFilter Filter { get; }
        public Socks4ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource) : this(iPEndPoint, proxySource, new Socks4ProxyServerFilter())
        {

        }
        public Socks4ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, Socks4ProxyServerFilter filter)
            : base(iPEndPoint, proxySource, filter)
        {
            this.Filter = filter;
        }

        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new Socks4ProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken).ProxyWorkAsync();
        }
    }
}
