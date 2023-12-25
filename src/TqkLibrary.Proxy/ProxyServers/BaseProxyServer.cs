using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxyServers
{
    public abstract class BaseProxyServer : IProxyServer
    {
        public IPEndPoint? IPEndPoint { get { return _tcpListener.LocalEndpoint as IPEndPoint; } }
        public int Timeout { get; set; } = 30000;


        protected readonly ILogger? _logger;
        readonly TcpListener _tcpListener;
        readonly BaseProxyServerHandler _baseProxyServerHandler;
        readonly object _lock_cancellationToken = new object();
        CancellationToken _CancellationToken
        {
            get { lock (_lock_cancellationToken) return _cancellationTokenSource.Token; }
        }


        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();


        protected BaseProxyServer(
            IPEndPoint iPEndPoint,
            BaseProxyServerHandler handler
            )
        {
            this._baseProxyServerHandler = handler ?? throw new ArgumentNullException(nameof(handler));
            this._tcpListener = new TcpListener(iPEndPoint);
            _logger = Singleton.LoggerFactory?.CreateLogger(this.GetType());
        }
        ~BaseProxyServer()
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
            StopListen();
            _ShutdownCurrentConnection(false);
        }


        public void StartListen(bool allowNatTraversal = false)
        {
            if (!this._tcpListener.Server.IsBound)
            {
                this._tcpListener.AllowNatTraversal(allowNatTraversal);
                this._tcpListener.Start();
                Task.Run(_MainLoopListen);
            }
        }
        public void StopListen()
        {
            if (this._tcpListener.Server.IsBound)
                this._tcpListener.Stop();
        }
        public void ShutdownCurrentConnection()
        {
            _ShutdownCurrentConnection(true);
        }
        void _ShutdownCurrentConnection(bool createNewCancellationToken)
        {
            lock (_lock_cancellationToken)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                if (createNewCancellationToken) _cancellationTokenSource = new CancellationTokenSource();
            }
        }



        async void _MainLoopListen()
        {
            while (this._tcpListener.Server.IsBound)
            {
                try
                {
                    TcpClient tcpClient = await _tcpListener.AcceptTcpClientAsync();
                    _ = _PreProxyWorkAsync(tcpClient);//run in task
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, nameof(_MainLoopListen));
                }
            }
        }


        private async Task _PreProxyWorkAsync(TcpClient tcpClient)
        {
            try
            {
                using (tcpClient)
                {
                    if (await _baseProxyServerHandler.IsAcceptClientFilterAsync(tcpClient, _CancellationToken))
                    {
                        using Stream stream = await _baseProxyServerHandler.StreamFilterAsync(tcpClient.GetStream(), _CancellationToken);
                        await ProxyWorkAsync(stream, tcpClient.Client.RemoteEndPoint!, _CancellationToken);
                    }
                }
            }
            catch(ObjectDisposedException ode)
            {
                _logger?.LogInformation(ode, nameof(_PreProxyWorkAsync));
            }
            catch (OperationCanceledException oce)
            {
                _logger?.LogInformation(oce, nameof(_PreProxyWorkAsync));
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, nameof(_PreProxyWorkAsync));
            }
        }

        protected abstract Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default);

    }
}
