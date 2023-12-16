using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Filters;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxyServers
{
    public abstract class BaseProxyServer : IProxyServer
    {
        public IPEndPoint IPEndPoint { get; }
        public IProxySource ProxySource { get; private set; }
        public int Timeout { get; set; } = 30000;


        readonly TcpListener _tcpListener;
        readonly BaseProxyServerFilter _baseProxyServerFilter;
        readonly object _lock_cancellationToken = new object();
        CancellationToken _CancellationToken
        {
            get { lock (_lock_cancellationToken) return _cancellationTokenSource.Token; }
        }


        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();


        protected BaseProxyServer(
            IPEndPoint iPEndPoint,
            IProxySource proxySource,
            BaseProxyServerFilter baseProxyServerFilter
            )
        {
            this._baseProxyServerFilter = baseProxyServerFilter ?? throw new ArgumentNullException(nameof(baseProxyServerFilter));
            this.ProxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
            this._tcpListener = new TcpListener(iPEndPoint);
            this.IPEndPoint = iPEndPoint;
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


        public void StartListen()
        {
            if (!this._tcpListener.Server.IsBound)
            {
                this._tcpListener.Start();
                Task.Run(_MainLoopListen);
            }
        }
        public void StopListen()
        {
            if (this._tcpListener.Server.IsBound)
                this._tcpListener.Stop();
        }
        public void ChangeSource(IProxySource proxySource, bool isShutdownCurrentConnection = false)
        {
            if (proxySource is null) throw new ArgumentNullException(nameof(proxySource));
            this.ProxySource = proxySource;
            if (isShutdownCurrentConnection) ShutdownCurrentConnection();
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
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
                _cancellationTokenSource = null;
#pragma warning restore CS8625 // Cannot convert null literal to non-nullable reference type.
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
#if DEBUG
                    Console.WriteLine($"[{nameof(BaseProxyServer)}.{nameof(_MainLoopListen)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
            }
        }


        private async Task _PreProxyWorkAsync(TcpClient tcpClient)
        {
            using (tcpClient)
            {
                if (await _baseProxyServerFilter.IsAcceptClientFilterAsync(tcpClient, _CancellationToken))
                {
                    using Stream stream = await _baseProxyServerFilter.StreamFilterAsync(tcpClient.GetStream(), _CancellationToken);
                    await ProxyWorkAsync(stream, tcpClient.Client.RemoteEndPoint!, _CancellationToken);
                }
            }
        }

        protected abstract Task ProxyWorkAsync(Stream clientStream, EndPoint clientEndPoint, CancellationToken cancellationToken = default);

    }
}
