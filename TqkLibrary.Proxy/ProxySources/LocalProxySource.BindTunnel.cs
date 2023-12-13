using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class BindTunnel : BaseTunnel, IBindSource
        {
            readonly TcpListener _tcpListener;
            TcpClient _tcpClient;
            internal BindTunnel(
                LocalProxySource proxySource,
                CancellationToken cancellationToken = default)
                : base(proxySource, cancellationToken)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                try { _tcpListener.Stop(); } catch { }
                _tcpClient?.Dispose();
                _tcpClient = null;
                base.Dispose(isDisposing);
            }

            internal async Task<IBindSource> InitBindAsync(Uri address)
            {
                if (address is null) throw new ArgumentNullException(nameof(address));

                //check if socks4, mustbe return ipv4. 
                //on socks5, return ipv6 if have & need 
                if (address.HostNameType != UriHostNameType.IPv4 || address.HostNameType != UriHostNameType.IPv6)
                    throw new InvalidDataException($"{nameof(address)} mustbe {nameof(UriHostNameType.IPv4)} or {nameof(UriHostNameType.IPv6)}");

                IPAddress ipAddress = _proxySource.BindIpAddress;
                if (_InvalidIPAddresss.Any(x => x == ipAddress))
                {
                    ipAddress = null;

                    //if invalid 
                    var addresses_s = await _GetLocalIpAddress();

                    if (address.HostNameType == UriHostNameType.IPv6)
                        ipAddress = addresses_s.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6);

                    if (ipAddress is null)
                        ipAddress = addresses_s.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

                    if (ipAddress is null)
                        return null;
                }



                return this;
            }



            public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
            {
                _tcpListener.Start();
                return Task.FromResult<IPEndPoint>((IPEndPoint)_tcpListener.LocalEndpoint);
            }
            public async Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
            {
                if (_tcpClient is null)
                {
                    TaskCompletionSource<object> tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using var register = cancellationToken.Register(() => tcs.TrySetCanceled());
                    AsyncCallback asyncCallback = (IAsyncResult ar) =>
                    {
                        _tcpClient = _tcpListener.EndAcceptTcpClient(ar);
                        tcs.TrySetResult(null);
                    };
                    _tcpListener.BeginAcceptTcpClient(asyncCallback, null);
                    await tcs.Task;
                }
                return _tcpClient?.GetStream();
            }






            static async Task<IPAddress[]> _GetLocalIpAddress()
            {
                IPHostEntry iPHostEntry = await Dns.GetHostEntryAsync(Dns.GetHostName());
                return iPHostEntry.AddressList;
            }
            static readonly IEnumerable<IPAddress> _InvalidIPAddresss = new IPAddress[]
            {
                null,
                IPAddress.Any,
                IPAddress.Loopback,
                IPAddress.Broadcast,
                IPAddress.IPv6Any,
                IPAddress.IPv6Loopback,
            };
        }
    }
}
