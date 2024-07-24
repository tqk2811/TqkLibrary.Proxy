using System.Net;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Helpers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class Socks5ProxySource
    {
        public class BindTunnel : BaseTunnel, IBindSource
        {
            internal protected BindTunnel(Socks5ProxySource proxySource, Guid tunnelId) : base(proxySource, tunnelId)
            {

            }

            public virtual async Task<IPEndPoint> BindAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                await ConnectAndAuthAsync();

                Socks5_Request socks5_Connection = Socks5_Request.CreateBind();
                await _stream!.WriteAsync(socks5_Connection.GetByteArray(), cancellationToken);
                await _stream!.FlushAsync(cancellationToken);

                Socks5_RequestResponse socks5_RequestResponse = await _stream!.Read_Socks5_RequestResponse_Async(cancellationToken);

                if (socks5_RequestResponse.STATUS != Socks5_STATUS.RequestGranted)
                {
                    throw new InitBindSourceFailedException($"{nameof(Socks5_STATUS)}: {socks5_RequestResponse.STATUS}");
                }
                if (socks5_RequestResponse.BNDADDR.ATYP == Socks5_ATYP.DomainName)
                {
                    throw new InvalidOperationException($"socks5 bind response support ipv4/ipv6 only");
                }


                if (socks5_RequestResponse.BNDADDR.IPAddress.Equals(IPAddress.Any) || socks5_RequestResponse.BNDADDR.IPAddress.Equals(IPAddress.IPv6Any))
                {
                    return new IPEndPoint(_proxySource.IPEndPoint.Address, socks5_RequestResponse.BNDPORT);
                }
                else
                {
                    return new IPEndPoint(socks5_RequestResponse.BNDADDR.IPAddress, socks5_RequestResponse.BNDPORT);
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
