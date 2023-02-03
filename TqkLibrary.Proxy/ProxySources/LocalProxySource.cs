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
    public class LocalProxySource : IProxySource, IHttpProxy
    {
        public bool IsSupportUdp { get; set; } = true;
        public bool IsSupportIpv6 { get; set; } = true;
        public bool IsSupportBind { get; set; } = true;

        public async Task<IConnectSource> InitConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            if (address == null) throw new NullReferenceException(nameof(address));

            switch (address.HostNameType)
            {
                case UriHostNameType.Dns://http://host/abc/def
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    {
                        switch (address.Scheme.ToLower())
                        {
                            case "http":// http proxy
                            case "https":

                            case "ws":// for http request (not CONNECT)
                            case "wss":

                            case "tcp":// socks4 / socks5
                                {
                                    TcpClient remote = new TcpClient();
                                    try
                                    {
#if NET5_0_OR_GREATER
                                        await remote.ConnectAsync(address.Host, address.Port, cancellationToken);
#else
                                        await remote.ConnectAsync(address.Host, address.Port);
#endif
                                        return new TcpStreamConnectSource(remote);
                                    }
                                    catch
                                    {
                                        remote.Dispose();
                                        return null;
                                    }
                                }

                            default:
                                throw new NotSupportedException(address.Scheme);
                        }

                    }

                default:
                    throw new NotSupportedException(address.HostNameType.ToString());
            }
        }

        public Task<IBindSource> InitBindAsync(Uri address, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }


        public Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
