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
        public IPAddress BindIpAddress { get; set; }

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

        async Task<IPAddress[]> GetLocalIpAddress()
        {
            IPHostEntry iPHostEntry = await Dns.GetHostEntryAsync(Dns.GetHostName());
            return iPHostEntry.AddressList;
        }
        static readonly IEnumerable<IPAddress> InvalidIPAddresss = new IPAddress[]
        {
            null,
            IPAddress.Any,
            IPAddress.Loopback,
            IPAddress.Broadcast,
            IPAddress.IPv6Any,
            IPAddress.IPv6Loopback,
        };
        public async Task<IBindSource> InitBindAsync(Uri address, CancellationToken cancellationToken = default)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));

            //check if socks4, mustbe return ipv4. 
            //on socks5, return ipv6 if have & need 
            if (address.HostNameType != UriHostNameType.IPv4 || address.HostNameType != UriHostNameType.IPv6)
                throw new InvalidDataException($"{nameof(address)} mustbe {nameof(UriHostNameType.IPv4)} or {nameof(UriHostNameType.IPv6)}");

            IPAddress ipAddress = BindIpAddress;
            if (InvalidIPAddresss.Any(x => x == ipAddress))
            {
                ipAddress = null;

                //if invalid 
                var addresses_s = await GetLocalIpAddress();

                if (address.HostNameType == UriHostNameType.IPv6)
                    ipAddress = addresses_s.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6);

                if (ipAddress is null)
                    ipAddress = addresses_s.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

                if (ipAddress is null)
                    return null;
            }

            return new BindSourceTunnel(this, ipAddress);
        }


        public Task<IUdpAssociateSource> InitUdpAssociateAsync(Uri address, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
