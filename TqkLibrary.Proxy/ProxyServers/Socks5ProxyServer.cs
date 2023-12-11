using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Filters;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks5ProxyServer : BaseProxyServer, ISocks5Proxy
    {
        public Socks5ProxyServerFilter Filter { get; }
        public Socks5ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource)
            : this(iPEndPoint, proxySource, new Socks5ProxyServerFilter())
        {

        }
        public Socks5ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, Socks5ProxyServerFilter filter)
            : base(iPEndPoint, proxySource, filter)
        {
            this.Filter = filter;
        }
        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new Socks5ProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken).ProxyWorkAsync();
        }
    }
}
