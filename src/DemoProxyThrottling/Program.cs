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


IProxySource proxySource = new LocalProxySource() { IsPrioritizeIpv4 = true };

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