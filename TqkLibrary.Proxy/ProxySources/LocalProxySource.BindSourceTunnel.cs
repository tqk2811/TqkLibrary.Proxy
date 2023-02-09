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
        class BindSourceTunnel : IBindSource
        {
            readonly LocalProxySource _localProxySource;
            readonly IPAddress _ipAddress;
            readonly TcpListener _tcpListener;
            TcpClient _tcpClient;
            internal BindSourceTunnel(LocalProxySource localProxySource, IPAddress ipAddress)
            {
                this._localProxySource = localProxySource ?? throw new ArgumentNullException(nameof(localProxySource));
                this._ipAddress = ipAddress ?? throw new ArgumentNullException(nameof(ipAddress));
                this._tcpListener = new TcpListener(ipAddress, 0);
            }
            ~BindSourceTunnel()
            {
                Dispose(false);
            }
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            void Dispose(bool disposing)
            {
                try { _tcpListener.Stop(); } catch { }
                _tcpClient?.Dispose();
                _tcpClient = null;
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
        }
    }
}
