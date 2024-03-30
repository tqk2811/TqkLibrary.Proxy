using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class ProxyServer : BaseLogger, IProxyServerListener
    {
        IPreProxyServerHandler _PreProxyServerHandler = new BasePreProxyServerHandler();
        /// <summary>
        /// 
        /// </summary>
        public IPreProxyServerHandler PreProxyServerHandler
        {
            get { return _PreProxyServerHandler; }
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(value));
                _PreProxyServerHandler = value;
            }
        }


        IProxyServerHandler _BaseProxyServerHandler = new BaseProxyServerHandler();
        /// <summary>
        /// 
        /// </summary>
        public IProxyServerHandler ProxyServerHandler
        {
            get { return _BaseProxyServerHandler; }
            set
            {
                if (value is null) throw new ArgumentNullException(nameof(value));
                _BaseProxyServerHandler = value;
            }
        }


        int _ReceiveTimeout = 0;
        public int ReceiveTimeout
        {
            get { return _ReceiveTimeout; }
            set
            {
                if (_ReceiveTimeout < 0) throw new IndexOutOfRangeException($"{nameof(ReceiveTimeout)} must be >= 0");
                _ReceiveTimeout = value;
            }
        }

        int _SendTimeout = 0;
        public int SendTimeout
        {
            get { return _SendTimeout; }
            set
            {
                if (_SendTimeout < 0) throw new IndexOutOfRangeException($"{nameof(_SendTimeout)} must be >= 0");
                _SendTimeout = value;
            }
        }

        public IPEndPoint? IPEndPoint { get { return _tcpListener.LocalEndpoint as IPEndPoint; } }




        readonly TcpListener _tcpListener;
        readonly object _lock_cancellationToken = new object();
        CancellationToken _CancellationToken
        {
            get { lock (_lock_cancellationToken) return _cancellationTokenSource.Token; }
        }
        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();


        public ProxyServer(
            IPEndPoint iPEndPoint
            )
        {
            _tcpListener = new TcpListener(iPEndPoint);
        }
        ~ProxyServer()
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="allowNatTraversal">Window Only</param>
        public void StartListen(bool allowNatTraversal = false)
        {
            if (!_tcpListener.Server.IsBound)
            {
#if NET5_0_OR_GREATER || NETSTANDARD
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
#endif
                {
                    _tcpListener.AllowNatTraversal(allowNatTraversal);
                }
                _tcpListener.Start();
                Task.Run(_MainLoopListen);
            }
        }
        public void StopListen()
        {
            if (_tcpListener.Server.IsBound)
                _tcpListener.Stop();
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
            while (_tcpListener.Server.IsBound)
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
                    tcpClient.ReceiveTimeout = ReceiveTimeout;
                    tcpClient.SendTimeout = SendTimeout;

                    IPEndPoint iPEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                    if (await PreProxyServerHandler.IsAcceptClientAsync(tcpClient, _CancellationToken))
                    {
                        using Stream baseStream = tcpClient.GetStream();
                        using AsynchronousOnlyStream asynchronousOnlyStream = new AsynchronousOnlyStream(baseStream);

                        using Stream stream = await PreProxyServerHandler.StreamHandlerAsync(asynchronousOnlyStream, iPEndPoint, _CancellationToken);
                        if (stream is null)
                            throw new InvalidOperationException($"{PreProxyServerHandler.GetType().FullName}.{nameof(IPreProxyServerHandler.StreamHandlerAsync)} was return null");

                        using PreReadStream preReadStream = new PreReadStream(stream);
                        IProxyServer proxyServer = await PreProxyServerHandler.GetProxyServerAsync(preReadStream, iPEndPoint, _CancellationToken);
                        if (proxyServer is null)
                            throw new InvalidOperationException($"{PreProxyServerHandler.GetType().FullName}.{nameof(IPreProxyServerHandler.GetProxyServerAsync)} was return null");

                        await proxyServer.ProxyWorkAsync(preReadStream, iPEndPoint, ProxyServerHandler, _CancellationToken);
                    }
                }
            }
            catch (ObjectDisposedException ode)
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
    }
}
