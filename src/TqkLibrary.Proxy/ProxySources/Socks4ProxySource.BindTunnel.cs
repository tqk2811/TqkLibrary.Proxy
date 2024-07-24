using System.Net;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks4ProxySource
    {
        public class BindTunnel : BaseTunnel, IBindSource
        {
            internal protected BindTunnel(Socks4ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }

            protected Socks4_RequestResponse? _socks4_RequestResponse;
            public virtual async Task<IPEndPoint> BindAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                await _ConnectToSocksServerAsync(cancellationToken);

                Socks4_Request socks4_Request = Socks4_Request.CreateBind(_proxySource.userId);

                byte[] buffer = socks4_Request.GetByteArray();
                await _stream!.WriteAsync(buffer, 0, buffer.Length, cancellationToken);

                _socks4_RequestResponse = await _stream.Read_Socks4_RequestResponse_Async(cancellationToken);
                if (_socks4_RequestResponse.REP != Socks4_REP.RequestGranted)
                {
                    throw new InitBindSourceFailedException($"{nameof(Socks4_REP)}: {_socks4_RequestResponse.REP}");
                }

                if (_socks4_RequestResponse.DSTIP.Equals(IPAddress.Any))
                {
                    return new IPEndPoint(_proxySource.iPEndPoint.Address, _socks4_RequestResponse.DSTPORT);
                }
                else
                {
                    return _socks4_RequestResponse.IPEndPoint;
                }
            }

            public virtual Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                if (_stream is null)
                    throw new InvalidOperationException($"Mustbe run {nameof(BindAsync)} first");
                CheckIsDisposed();

                return Task.FromResult(_stream);
            }
        }
    }
}
