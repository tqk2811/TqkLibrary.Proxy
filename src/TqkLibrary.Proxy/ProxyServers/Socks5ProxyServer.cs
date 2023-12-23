using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks5ProxyServer : BaseProxyServer, ISocks5Proxy
    {
        public Socks5ProxyServerHandler Handler { get; }
        public Socks5ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource)
            : this(iPEndPoint, new Socks5ProxyServerHandler(proxySource))
        {

        }
        public Socks5ProxyServer(IPEndPoint iPEndPoint, Socks5ProxyServerHandler handler)
            : base(iPEndPoint, handler)
        {
            this.Handler = handler;
        }
        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new Socks5ProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken).ProxyWorkAsync();
        }
    }
}
