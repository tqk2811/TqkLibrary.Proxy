using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHeplers;
using TqkLibrary.Proxy.Handlers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks4ProxyServer : BaseProxyServer, ISocks4Proxy
    {
        public Socks4ProxyServerHandler Handler { get; }
        public Socks4ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource) 
            : this(iPEndPoint, new Socks4ProxyServerHandler(proxySource))
        {

        }
        public Socks4ProxyServer(IPEndPoint iPEndPoint, Socks4ProxyServerHandler handler)
            : base(iPEndPoint, handler)
        {
            this.Handler = handler;
        }

        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new Socks4ProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken).ProxyWorkAsync();
        }
    }
}
