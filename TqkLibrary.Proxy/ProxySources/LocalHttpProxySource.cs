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

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalHttpProxySource : IProxySource, IHttpProxy
    {
        public bool IsSupportUdp => false;

        public bool IsSupportTransferHttps { get; } = true;

        public bool IsSupportIpv6 { get; } = true;

        public async Task<ISessionSource> InitSessionAsync(Uri address, string host = null)
        {
            if (address == null) throw new NullReferenceException(nameof(address));

            switch (address.HostNameType)
            {
                case UriHostNameType.Dns://http://host/abc/def
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    {
                        TcpClient remote = new TcpClient();
                        try
                        {
                            await remote.ConnectAsync(address.Host, address.Port);
                            return new HttpSessionSource(remote, host);
                        }
                        catch
                        {
                            remote.Dispose();
                            return null;
                        }
                    }

                default:
                    throw new NotSupportedException();
            }
        }
    }
}
