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
        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => true;
        public bool IsSupportBind => true;

        public Task<IBindSource> InitBindAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public async Task<IConnectionSource> InitConnectionAsync(Uri address, CancellationToken cancellationToken = default)
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

                            case "ws":// web socket
                            case "wss":

                            case "tcp":// socks4 / socks5
                                {
                                    TcpClient remote = new TcpClient();
                                    try
                                    {
                                        await remote.ConnectAsync(address.Host, address.Port);
                                        return new TcpStreamConnectionSource(remote);
                                    }
                                    catch
                                    {
                                        remote.Dispose();
                                        return null;
                                    }
                                }

                            case "udp":// socks5
                                {
                                    //UdpClient udpClient = new UdpClient();
                                    //try
                                    //{
                                    //    udpClient.Connect(address.Host, address.Port);
                                    //    return new StreamSessionSource(remote, host);
                                    //}
                                    //catch
                                    //{
                                    //    remote.Dispose();
                                    //    return null;
                                    //}
                                    return null;
                                }

                            default:
                                throw new NotSupportedException(address.Scheme);
                        }
                        
                    }

                default:
                    throw new NotSupportedException(address.HostNameType.ToString());
            }
        }
    }
}
