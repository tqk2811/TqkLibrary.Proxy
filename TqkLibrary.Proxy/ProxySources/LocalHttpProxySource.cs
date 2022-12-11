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

        public bool IsSupportTransferHttps { get; set; } = true;

        public bool IsSupportIpv6 { get; } = true;

        public Task<ISessionSource> InitSessionAsync(IPAddress iPAddress, ProtocolType protocolType)
        {
            throw new NotImplementedException();
        }

        public async Task<ISessionSource> InitSessionAsync(Uri address)
        {
            if (address == null) throw new NullReferenceException(nameof(address));

            switch (address.HostNameType)
            {
                case UriHostNameType.Unknown://Domain:port
                    {
                        TcpClient remote = new TcpClient();
                        try
                        {
                            await remote.ConnectAsync(address.Scheme, int.Parse(address.LocalPath));
                            return new HttpSessionSource(remote);
                        }
                        catch
                        {
                            remote.Dispose();
                            return null;
                        }
                    }
                case UriHostNameType.Dns://http://host/abc/def
                    {
                        TcpClient remote = new TcpClient();
                        try
                        {
                            int port = address.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
                            await remote.ConnectAsync(address.Host, port);
                            if(port == 443) return new HttpSessionSource(remote, address.Host);
                            else return new HttpSessionSource(remote);
                        }
                        catch
                        {
                            remote.Dispose();
                            return null;
                        }
                    }

                default:
                    Console.WriteLine($"InitSessionAsync[{address.HostNameType}]: {address.OriginalString}");
                    return null;
            }
        }
    }
}
