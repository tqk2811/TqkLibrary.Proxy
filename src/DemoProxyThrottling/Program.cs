// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Logging;
using System.Net;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Streams;
using TqkLibrary.Streams.ThrottlingHelpers;

TqkLibrary.Proxy.Singleton.LoggerFactory = LoggerFactory.Create(x => x.AddConsole());


IProxySource proxySource = new MyLocalProxySource();

ThrottlingConfigure throttlingConfigure = new ThrottlingConfigure();
throttlingConfigure.DelayStep = 0;
throttlingConfigure.Balanced = 1024;//balanced for multi stream
throttlingConfigure.ReadBytesPerTime = 100 * 1024;//100KiB/sec
throttlingConfigure.WriteBytesPerTime = 100 * 1024;//100KiB/sec
throttlingConfigure.Time = TimeSpan.FromSeconds(1);


using ProxyServer proxyServer = new ProxyServer(IPEndPoint.Parse("127.0.0.1:28111"), new MyProxyServerHandler(proxySource, throttlingConfigure));
proxyServer.PreProxyServerHandler = new MyPreProxyServerHandler(throttlingConfigure);
proxyServer.StartListen();
Console.WriteLine($"Listening: {proxyServer.IPEndPoint}");
Console.WriteLine("Press any key to exit");
Console.ReadLine();
proxyServer.StopListen();


class MyProxyServerHandler : BaseProxyServerHandler
{
    readonly ThrottlingConfigure _throttlingConfigure;
    public MyProxyServerHandler(IProxySource proxySource, ThrottlingConfigure throttlingConfigure) : base(proxySource)
    {
        this._throttlingConfigure = throttlingConfigure ?? throw new ArgumentNullException(nameof(throttlingConfigure));
    }

    //handle by userInfo
    //public override Task<Stream> StreamHandlerAsync(Stream stream, IUserInfo userInfo, CancellationToken cancellationToken = default)
    //{
    //    return Task.FromResult<Stream>(new ThrottlingStream(_throttlingConfigure, stream, true));
    //}
}
class MyPreProxyServerHandler : BasePreProxyServerHandler
{
    readonly ThrottlingConfigure _throttlingConfigure;
    public MyPreProxyServerHandler(ThrottlingConfigure throttlingConfigure)
    {
        this._throttlingConfigure = throttlingConfigure ?? throw new ArgumentNullException(nameof(throttlingConfigure));
    }

    //can edit handle by ip address
    public override Task<Stream> StreamHandlerAsync(Stream stream, IPEndPoint iPEndPoint, Guid tunnelId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<Stream>(new ThrottlingStream(_throttlingConfigure, stream, true));
    }
}
class MyLocalProxySource : LocalProxySource
{
    public override IConnectSource GetConnectSource(Guid tunnelId)
    {
        return new MyConnectTunnel(this, tunnelId);
    }

    class MyConnectTunnel : LocalProxySource.ConnectTunnel
    {
        protected internal MyConnectTunnel(LocalProxySource localProxySource, Guid tunnelId) : base(localProxySource, tunnelId)
        {

        }

        public override async Task ConnectAsync(Uri address, CancellationToken cancellationToken = default)
        {
            if (address is null)
                throw new ArgumentNullException(nameof(address));
            CheckIsDisposed();

            switch (address.HostNameType)
            {
                case UriHostNameType.Dns://http://host/abc/def
                    {
                        var ips = await Dns.GetHostAddressesAsync(address.Host);
                        ips = ips.OrderBy(x => x.AddressFamily).ToArray();//prioritize ipv4
                        await _tcpClient.ConnectAsync(ips, address.Port
#if NET5_0_OR_GREATER
                                , cancellationToken
#endif
                                );
                        _stream = _tcpClient.GetStream();
                        break;
                    }
                case UriHostNameType.IPv4:
                case UriHostNameType.IPv6:
                    {
                        if (!_proxySource.IsSupportIpv6 && address.HostNameType == UriHostNameType.IPv6)
                            throw new NotSupportedException($"IpV6 are not support");

                        if (_SupportUriSchemes.Any(x => x.Equals(address.Scheme, StringComparison.InvariantCulture)))
                        {
                            await _tcpClient.ConnectAsync(
                                address.Host,
                                address.Port
#if NET5_0_OR_GREATER
                                , cancellationToken
#endif
                            );
                            _stream = _tcpClient.GetStream();
                        }
                        else
                        {
                            throw new NotSupportedException(address.Scheme);
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException(address.HostNameType.ToString());
            }
        }
    }
}