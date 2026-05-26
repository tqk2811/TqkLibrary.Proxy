using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using TqkLibrary.Proxy.Handlers;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHelpers;
using TqkLibrary.Streams;

namespace TqkLibrary.Proxy
{
    public sealed class ProxyServer : IProxyServerListener
    {
        readonly ILoggerFactory? _loggerFactory;
        readonly ILogger? _logger;

        IPreProxyServerHandler _PreProxyServerHandler;
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
            IPEndPoint iPEndPoint,
            ILoggerFactory? loggerFactory = null
            )
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory?.CreateLogger<ProxyServer>();
            _tcpListener = new TcpListener(iPEndPoint);
            _PreProxyServerHandler = new BasePreProxyServerHandler(loggerFactory);
        }
        public ProxyServer(
            IPEndPoint iPEndPoint,
            IProxyServerHandler proxyServerHandler,
            ILoggerFactory? loggerFactory = null
            ) : this(iPEndPoint, loggerFactory)
        {
            this.ProxyServerHandler = proxyServerHandler ?? throw new ArgumentNullException(nameof(proxyServerHandler));
        }
        public ProxyServer(
            IPEndPoint iPEndPoint,
            IProxySource proxySource,
            ILoggerFactory? loggerFactory = null
            ) : this(iPEndPoint, new BaseProxyServerHandler(proxySource ?? throw new ArgumentNullException(nameof(proxySource))), loggerFactory)
        {

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
#if NET8_0_OR_GREATER
            _tcpListener.Dispose();
#endif
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
                    _logger?.LogCritical(ex, "Accept loop failed");
                }
            }
        }


        private async Task _PreProxyWorkAsync(TcpClient tcpClient)
        {
            Guid tunnelId = Guid.NewGuid();
            IPEndPoint clientEndPoint = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;

            using IDisposable? scope = _logger?.BeginScope(new Dictionary<string, object>
            {
                ["TunnelId"] = tunnelId,
                ["ClientEndPoint"] = clientEndPoint,
            });

            try
            {
                using (tcpClient)
                {
                    tcpClient.ReceiveTimeout = ReceiveTimeout;
                    tcpClient.SendTimeout = SendTimeout;

                    if (await PreProxyServerHandler.IsAcceptClientAsync(tcpClient, tunnelId, _CancellationToken))
                    {
                        using Stream baseStream = tcpClient.GetStream();

                        using Stream stream = await PreProxyServerHandler.StreamHandlerAsync(baseStream, clientEndPoint, tunnelId, _CancellationToken);
                        if (stream is null)
                            throw new InvalidOperationException($"{PreProxyServerHandler.GetType().FullName}.{nameof(IPreProxyServerHandler.StreamHandlerAsync)} was return null");

                        using PreReadStream preReadStream = new PreReadStream(stream);
                        IProxyServer proxyServer = await PreProxyServerHandler.GetProxyServerAsync(preReadStream, clientEndPoint, tunnelId, _CancellationToken);
                        if (proxyServer is null)
                            throw new InvalidOperationException($"{PreProxyServerHandler.GetType().FullName}.{nameof(IPreProxyServerHandler.GetProxyServerAsync)} was return null");

                        // Inner try-catch around ProxyWorkAsync only: runs BEFORE the wrapper `using`
                        // disposes, so OnExceptionAsync can tag failure state before stream.Dispose
                        // commits the final log row (e.g. a bytes-counting wrapper).
                        // GetProxyServerAsync is intentionally outside — handlers own that hook's
                        // failure semantics directly (they can tag state themselves on throw), no
                        // need for the library to also raise OnExceptionAsync for it.
                        try
                        {
                            await proxyServer.ProxyWorkAsync(preReadStream, clientEndPoint, ProxyServerHandler, tunnelId, _CancellationToken);
                        }
                        catch (Exception innerEx)
                        {
                            await _SafeInvokeOnExceptionAsync(clientEndPoint, tunnelId, innerEx);
                            throw;
                        }
                    }
                }
            }
            catch (ObjectDisposedException ode)
            {
                _logger?.LogInformation(ode, "Tunnel work canceled (ObjectDisposed)");
            }
            catch (OperationCanceledException oce)
            {
                _logger?.LogInformation(oce, "Tunnel work canceled");
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, "Tunnel work failed");
            }
        }

        async Task _SafeInvokeOnExceptionAsync(IPEndPoint clientEndPoint, Guid tunnelId, Exception ex)
        {
            try
            {
                await PreProxyServerHandler.OnExceptionAsync(clientEndPoint, tunnelId, ex);
            }
            catch (Exception hookEx)
            {
                _logger?.LogError(hookEx, "PreProxyServerHandler.OnExceptionAsync threw");
            }
        }
    }
}
