using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public class HttpProxySource : IProxySource, IHttpProxy
    {
        readonly Uri proxy;
        readonly NetworkCredential networkCredential;
        public HttpProxySource(Uri proxy)
        {
            this.proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        }
        /// <summary>
        /// Self host
        /// </summary>
        public HttpProxySource(Uri proxy, NetworkCredential networkCredential) : this(proxy)
        {
            this.networkCredential = networkCredential ?? throw new ArgumentNullException(nameof(networkCredential));
        }

        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => true;

        public async Task<ISessionSource> InitSessionAsync(Uri address, string host = null)
        {
            if (address == null) throw new ArgumentNullException(nameof(address));
            //allway use connect

            TcpClient tcpClient = new TcpClient();
            Stream networkStream = null;
            HeaderResponseParse headerResponseParse = null;
            try
            {
                await tcpClient.ConnectAsync(proxy.Host, proxy.Port);
                networkStream = tcpClient.GetStream();

                await networkStream.WriteLineAsync($"CONNECT {address} HTTP/1.1");
#if DEBUG
                Console.WriteLine($"[{nameof(HttpProxySource)}.{nameof(InitSessionAsync)}] {proxy.Host}:{proxy.Port} << CONNECT {address} HTTP/1.1");
#endif
                if (networkCredential != null)
                {
                    string data = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{networkCredential.UserName}:{networkCredential.Password}"));
                    await networkStream.WriteLineAsync($"Proxy-Authorization: Basic {data}");
#if DEBUG
                    Console.WriteLine($"[{nameof(HttpProxySource)}.{nameof(InitSessionAsync)}] {proxy.Host}:{proxy.Port} << Proxy-Authorization: Basic {data}");
#endif
                }
                await networkStream.WriteLineAsync();
                await networkStream.FlushAsync();

                List<string> response_HeaderLines = await networkStream.ReadHeader();
#if DEBUG
                response_HeaderLines.ForEach(x =>
                    Console.WriteLine($"[{nameof(HttpProxySource)}.{nameof(InitSessionAsync)}] {proxy.Host}:{proxy.Port} >> {x}"));                
#endif
                headerResponseParse = response_HeaderLines.ParseResponse();

                if (headerResponseParse.HttpStatusCode == HttpStatusCode.OK)
                {
                    return new TcpStreamSessionSource(tcpClient);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{nameof(HttpProxySource)}.{nameof(InitSessionAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
            }
            finally
            {
                if (headerResponseParse?.HttpStatusCode != HttpStatusCode.OK)
                {
                    networkStream?.Dispose();
                    networkStream = null;
                    tcpClient?.Dispose();
                    tcpClient = null;
                }
            }
            return null;
        }
    }
}
