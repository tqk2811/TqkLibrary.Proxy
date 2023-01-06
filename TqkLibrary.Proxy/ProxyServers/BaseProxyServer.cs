using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxyServers
{
    public abstract class BaseProxyServer : IProxyServer
    {
        readonly TcpListener tcpListener;
        public IPEndPoint IPEndPoint { get; }

        public IProxySource ProxySource { get; }
        public int Timeout { get; set; } = 30000;

        IAsyncResult asyncResult;

        protected BaseProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource)
        {
            this.ProxySource = proxySource ?? throw new ArgumentNullException(nameof(proxySource));
            this.tcpListener = new TcpListener(iPEndPoint);
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
        }


        public void ShutdownCurrentConnection()
        {
            throw new NotImplementedException();
        }

        public void StartListen()
        {
            lock (tcpListener)
            {
                this.tcpListener.Start();
                asyncResult = this.tcpListener.BeginAcceptTcpClient(BeginAcceptTcpClientAsyncCallback, null);
            }
        }

        void BeginAcceptTcpClientAsyncCallback(IAsyncResult ar)
        {
            try
            {
                if (this.tcpListener.Server.IsBound)
                {
                    lock (tcpListener)
                    {
                        TcpClient tcpClient = this.tcpListener.EndAcceptTcpClient(ar);
                        _ = PreProxyWork(tcpClient);//run in task
                        asyncResult = this.tcpListener.BeginAcceptTcpClient(BeginAcceptTcpClientAsyncCallback, null);
                    }
                }
#if DEBUG
                else
                {
                    Console.WriteLine($"[{nameof(BaseProxyServer)}.{nameof(BeginAcceptTcpClientAsyncCallback)}] Stopped Listen");
                }
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($"[{nameof(BaseProxyServer)}.{nameof(BeginAcceptTcpClientAsyncCallback)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
            }
        }

        public void StopListen()
        {
            lock (tcpListener)
            {
                try
                {
                    this.tcpListener.Stop();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(BaseProxyServer)}.{nameof(StopListen)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                }
            }
        }


        private async Task PreProxyWork(TcpClient tcpClient)
        {
            using (tcpClient)
            {
                using NetworkStream networkStream = tcpClient.GetStream();
                await ProxyWorkAsync(networkStream, tcpClient.Client.RemoteEndPoint);
            }
        }
        protected abstract Task ProxyWorkAsync(Stream client_stream, EndPoint client_EndPoint, CancellationToken cancellationToken = default);

    }
}
