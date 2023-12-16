using System.Net;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        //class BindTunnel : BaseTunnel, IBindSource
        //{
        //    internal BindTunnel(Socks5ProxySource proxySource) : base(proxySource)
        //    {

        //    }

        //    public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
        //    {
        //        if (_socks5_RequestResponse is null) throw new InvalidOperationException();
        //        return Task.FromResult(_socks5_RequestResponse.IPEndPoint);
        //    }

        //    public Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
        //    {
        //        if (_stream is null) throw new InvalidOperationException();
        //        return Task.FromResult(_stream);
        //    }

        //    public Task InitAsync(Uri address, CancellationToken cancellationToken = default)
        //    {
        //        throw new NotImplementedException();
        //    }





        //    internal async Task<IBindSource> InitBindAsync(Uri address)
        //    {
        //        await InitAsync();
        //        if (await BindRequestAsync(address))
        //        {
        //            return this;
        //        }
        //        throw new Exception();
        //    }

        //    Socks5_RequestResponse? _socks5_RequestResponse = null;
        //    async Task<bool> BindRequestAsync(Uri address)
        //    {
        //        if (_proxySource.IsSupportBind)
        //        {
        //            if (_stream is null) throw new InvalidOperationException();

        //            Socks5_Request socks5_Connection = new Socks5_Request(Socks5_CMD.EstablishPortBinding, address);
        //            await _stream.WriteAsync(socks5_Connection.GetByteArray(), _cancellationToken);
        //            await _stream.FlushAsync(_cancellationToken);

        //            _socks5_RequestResponse = await _stream.Read_Socks5_RequestResponse_Async(_cancellationToken);
        //            if (_socks5_RequestResponse.STATUS == Socks5_STATUS.RequestGranted)
        //            {
        //                return true;
        //            }
        //        }

        //        return false;
        //    }

        //}
    }
}
