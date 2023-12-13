using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxyServers;

namespace TqkLibrary.Proxy.StreamHeplers
{
    internal class TcpStreamConnectSource : IConnectSource
    {
        readonly TcpClient tcpClient;
        readonly string host;
        public TcpStreamConnectSource(TcpClient tcpClient, string host = null)
        {
            this.tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            this.host = host;
        }
        ~TcpStreamConnectSource()
        {
            tcpClient.Dispose();
        }
        public void Dispose()
        {
            tcpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public Task<Stream> GetStreamAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_GetStream());


        Stream _GetStream()
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return tcpClient.GetStream();
            }
            else
            {
                var stream = new SslStream(
                    tcpClient.GetStream(),
                    false,
                    null,//_RemoteCertificateValidationCallback,
                    null//_LocalCertificateSelectionCallback
                    );
                try
                {
                    stream.AuthenticateAsClient(host);
                    return stream;
                }
                catch (AuthenticationException)
                {
                    stream.Dispose();
                    throw;
                }
            }
        }

        bool _RemoteCertificateValidationCallback(
            object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return false;
        }
        X509Certificate _LocalCertificateSelectionCallback(
            object sender,
            string targetHost,
            X509CertificateCollection localCertificates,
            X509Certificate remoteCertificate,
            string[] acceptableIssuers)
        {
            return null;
        }
    }
}
