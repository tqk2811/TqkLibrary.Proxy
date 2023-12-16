using System.Net;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        //class BindTunnel : BaseTunnel, IBindSource
        //{
        //    internal BindTunnel(Socks4ProxySource proxySource) : base(proxySource)
        //    {

        //    }

        //    Socks4_RequestResponse? _socks4_RequestResponse;
        //    public async Task InitAsync(Uri address, CancellationToken cancellationToken = default)
        //    {
        //        await _ConnectToSocksServerAsync();

        //        Socks4_Request socks4_Request = new Socks4_Request(Socks4_CMD.Bind, address, _proxySource.userId);

        //        byte[] buffer = socks4_Request.GetByteArray();
        //        await this._stream!.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

        //        _socks4_RequestResponse = await this._stream.Read_Socks4_RequestResponse_Async(cancellationToken);
        //        if (_socks4_RequestResponse.REP != Socks4_REP.RequestGranted)
        //        {
        //            throw new InitConnectSourceFailedException($"{nameof(Socks4_REP)}: {_socks4_RequestResponse.REP}");
        //        }
        //    }

        //    public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
        //    {
        //        if (_socks4_RequestResponse is null)
        //            throw new InvalidOperationException();

        //        return Task.FromResult(_socks4_RequestResponse.IPEndPoint);
        //    }

        //    public Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
        //    {
        //        if (_stream is null)
        //            throw new InvalidOperationException();

        //        return Task.FromResult(_stream);
        //    }

        //}
    }
}
