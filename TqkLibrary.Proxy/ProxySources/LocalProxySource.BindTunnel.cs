using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class BindTunnel : BaseTunnel, IBindSource
        {
            readonly TcpListener _tcpListener;
            private BindTunnel(
                LocalProxySource proxySource,
                TcpListener tcpListener,
                CancellationToken cancellationToken = default)
                : base(proxySource, cancellationToken)
            {
                this._tcpListener = tcpListener ?? throw new ArgumentNullException(nameof(tcpListener));
            }
            protected override void Dispose(bool isDisposing)
            {
                try { _tcpListener?.Stop(); } catch { }
                base.Dispose(isDisposing);
            }

            public Task<IPEndPoint> InitListenAsync(CancellationToken cancellationToken = default)
            {
                _tcpListener.Start();
                return Task.FromResult<IPEndPoint>((IPEndPoint)_tcpListener.LocalEndpoint);
            }
            public async Task<Stream> WaitConnectionAsync(CancellationToken cancellationToken = default)
            {
                return new TcpClientStreamWrapper(await _WaitClientAsync(cancellationToken));
            }

            async Task<TcpClient> _WaitClientAsync(CancellationToken cancellationToken = default)
            {
                if (_tcpListener is null) throw new InvalidOperationException();

                TaskCompletionSource<TcpClient> tcs = new TaskCompletionSource<TcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var register = cancellationToken.Register(() => tcs.TrySetCanceled());
                AsyncCallback asyncCallback = (IAsyncResult ar) =>
                {
                    tcs.TrySetResult(_tcpListener.EndAcceptTcpClient(ar));
                };
                _tcpListener.BeginAcceptTcpClient(asyncCallback, null);
                return await tcs.Task;
            }





            internal static async Task<IBindSource> _InitBindAsync(
                LocalProxySource proxySource,
                Uri address,
                CancellationToken cancellationToken = default)
            {
                if (proxySource is null) throw new ArgumentNullException(nameof(proxySource));
                if (address is null) throw new ArgumentNullException(nameof(address));

                //check if socks4, mustbe return ipv4. 
                //on socks5, return ipv6 if have & need 
                if (address.HostNameType != UriHostNameType.IPv4 || address.HostNameType != UriHostNameType.IPv6)
                    throw new InvalidDataException($"{nameof(address)} mustbe {nameof(UriHostNameType.IPv4)} or {nameof(UriHostNameType.IPv6)}");

                IPAddress? ipAddress = proxySource.BindIpAddress;
                if (_InvalidIPAddresss.Contains(ipAddress))
                {
                    ipAddress = null;

                    //if invalid 
                    var addresses_s = await _GetLocalIpAddress();

                    if (address.HostNameType == UriHostNameType.IPv6)
                        ipAddress = addresses_s.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetworkV6);

                    if (ipAddress is null)
                        ipAddress = addresses_s.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);

                    if (ipAddress is null)
                        throw new InvalidOperationException($"{nameof(LocalProxySource)}.{nameof(LocalProxySource.BindIpAddress)} must be set");
                }
                TcpListener tcpListener = new TcpListener(ipAddress, proxySource.BindListenPort);

                return new BindTunnel(proxySource, tcpListener, cancellationToken);
            }
            static async Task<IPAddress[]> _GetLocalIpAddress()
            {
                IPHostEntry iPHostEntry = await Dns.GetHostEntryAsync(Dns.GetHostName());
                return iPHostEntry.AddressList;
            }
            static readonly IEnumerable<IPAddress?> _InvalidIPAddresss = new IPAddress?[]
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
