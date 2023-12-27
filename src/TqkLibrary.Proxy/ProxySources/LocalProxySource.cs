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
        /// <summary>
        /// default or null: <see cref="IPAddress.Any"/>
        /// </summary>
        public IPAddress? BindIpAddress { get; set; }
        public UInt16 BindListenPort { get; set; } = 0;
        /// <summary>
        /// window only
        /// </summary>
        public bool IsAllowNatTraversal { get; set; } = false;

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
