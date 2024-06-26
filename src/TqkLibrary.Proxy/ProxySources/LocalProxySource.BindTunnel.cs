﻿using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using TqkLibrary.Proxy.Exceptions;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource
    {
        class BindTunnel : BaseTunnel, IBindSource
        {
            TcpListener? _tcpListener;
            TcpClient? _tcpClient;
            internal BindTunnel(LocalProxySource proxySource)
                : base(proxySource)
            {

            }
            protected override void Dispose(bool isDisposing)
            {
                try { _tcpListener?.Stop(); } catch { }
                _tcpClient?.Dispose();
                _tcpClient = null;
                base.Dispose(isDisposing);
            }

            public async Task<IPEndPoint> BindAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                if (_tcpListener is null)
                {
                    _tcpListener = new TcpListener(await _proxySource.GetListenEndPointAsync());
#if NET5_0_OR_GREATER || NETSTANDARD
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                    {
                        _tcpListener.AllowNatTraversal(_proxySource.IsAllowNatTraversal);
                    }
                    _tcpListener.Start();
                    _tcpListener.BeginAcceptTcpClient(OnBeginAcceptTcpClient, null);
                }

                return new IPEndPoint(await _proxySource.GetResponseIPAddressAsync(), ((IPEndPoint)_tcpListener.LocalEndpoint).Port);
            }

            public async Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            {
                CheckIsDisposed();
                if (_tcpListener is null)
                    throw new InvalidOperationException($"Must run {nameof(BindAsync)} first");

                TaskCompletionSource<TcpClient> tcs = new TaskCompletionSource<TcpClient>(TaskCreationOptions.RunContinuationsAsynchronously);
                Action<TcpClient> action = (client) => tcs.TrySetResult(client);

                using CancellationTokenSource cts = new CancellationTokenSource(_proxySource.BindListenTimeout);
                using var register = cts.Token.Register(() => tcs.TrySetException(new BindListenTimeoutException()));
                try
                {
                    OnEndAcceptTcpClient += action;
                    if (_tcpClient is not null) tcs.TrySetResult(_tcpClient);
                    await tcs.Task;
                    return _tcpClient!.GetStream();
                }
                finally
                {
                    OnEndAcceptTcpClient -= action;
                }

            }

            private event Action<TcpClient>? OnEndAcceptTcpClient;
            void OnBeginAcceptTcpClient(IAsyncResult ar)
            {
                try
                {
                    _tcpClient = _tcpListener!.EndAcceptTcpClient(ar);
                    OnEndAcceptTcpClient?.Invoke(_tcpClient);
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, "OnBeginAcceptTcpClient");
                }
            }
        }
    }
}
