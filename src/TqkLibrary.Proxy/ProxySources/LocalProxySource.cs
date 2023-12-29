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
using System.Threading;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource : IProxySource, IHttpProxy
    {
        public LocalProxySourceHandler Handler { get; }
        public LocalProxySource()
        {
            Handler = new LocalProxySourceHandler();
        }
        public LocalProxySource(LocalProxySourceHandler handler)
        {
            this.Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public bool IsSupportUdp { get; set; } = true;
        public bool IsSupportIpv6 { get; set; } = true;
        public bool IsSupportBind { get; set; } = true;
        /// <summary>
        /// window only
        /// </summary>
        public bool IsAllowNatTraversal { get; set; } = false;
        public int BindListenTimeout { get; set; } = 30000;

        public IConnectSource GetConnectSource()
        {
            return new ConnectTunnel(this);
        }

        public IBindSource GetBindSource()
        {
            return new BindTunnel(this);
        }

        public IUdpAssociateSource GetUdpAssociateSource()
        {
            throw new NotSupportedException();
            //return new UdpTunnel(this);
        }
    }
}
