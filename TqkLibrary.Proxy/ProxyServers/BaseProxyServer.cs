﻿using System;
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
                lock (tcpListener)
                {
                    TcpClient tcpClient = this.tcpListener.EndAcceptTcpClient(ar);
                    ProxyWork(tcpClient);//run in task
                    asyncResult = this.tcpListener.BeginAcceptTcpClient(BeginAcceptTcpClientAsyncCallback, null);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($"{ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
            }
        }

        public void StopListen()
        {
            lock (tcpListener)
            {
                this.tcpListener.EndAcceptTcpClient(asyncResult);
                asyncResult = null;
                this.tcpListener.Stop();
            }
        }

        protected abstract Task ProxyWork(TcpClient tcpClient);
    }
}
