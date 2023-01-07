using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public partial class Socks5ProxyServer : BaseProxyServer, ISocks5Proxy
    {
        public Socks5ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, NetworkCredential networkCredential) : base(iPEndPoint, proxySource)
        {
            this.NetworkCredential = networkCredential;
        }
        public NetworkCredential NetworkCredential { get; set; }

        protected override Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default)
        {
            return new Socks5ProxyServerTunnel(this, clientStream, clientEndPoint, cancellationToken).ProxyWorkAsync();
        }
    }
}
