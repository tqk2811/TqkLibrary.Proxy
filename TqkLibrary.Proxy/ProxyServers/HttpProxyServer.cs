using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using System.Text.RegularExpressions;
using System.Net.Http;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class HttpProxyServer : BaseProxyServer, IHttpProxy
    {
        static readonly HttpClientHandler httpClientHandler;
        static readonly HttpClient httpClient;
        static HttpProxyServer()
        {
            httpClientHandler = new HttpClientHandler()
            {
                UseCookies = false,
                UseProxy = false,
            };
            httpClient = new HttpClient(httpClientHandler, true);
        }


        public bool AllowHttps { get; set; } = true;
        public ICredentials Credentials { get; }
        public HttpProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource, ICredentials credentials = null) : base(iPEndPoint, proxySource)
        {
            this.Credentials = credentials;
        }

        protected override async Task ProxyWork(TcpClient tcpClient)
        {
            using (tcpClient)
            {
                using NetworkStream networkStream = tcpClient.GetStream();
                using StreamReader streamReader = new StreamReader(networkStream, Encoding.ASCII);
                using StreamWriter streamWriter = new StreamWriter(networkStream);

                bool isKeepAlive = false;
                do
                {
                    List<string> lines = await streamReader.ReadHeader();
#if DEBUG
                    lines.ForEach(x => Console.WriteLine($"{tcpClient.Client.RemoteEndPoint} => {x}"));
#endif
                    HeaderParse headerParse = lines.Parse();

                    //Check Proxy-Authorization
                    if (Credentials != null)
                    {
                        if (headerParse.ProxyAuthorization == null)
                        {
                            await WriteResponse(tcpClient, streamWriter, "407 Proxy Authentication Required");
                            return;
                        }
                        else
                        {

                        }
                    }

                    isKeepAlive = headerParse.IsKeepAlive;

                    if ("CONNECT".Equals(headerParse.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        //https proxy
                        if (AllowHttps)
                        {
                            using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(headerParse.Uri, true);
                            if (sessionSource == null)
                            {
                                await WriteResponse(tcpClient, streamWriter, "408 Request Timeout");
                                return;
                            }
                            else
                            {
                                await WriteResponse(tcpClient, streamWriter, "200 Connection established");
                            }

                            using var remote_stream = sessionSource.GetStream();
                            await new StreamTransferHelper(networkStream, remote_stream).WaitUntilDisconnect().ConfigureAwait(false);
                        }
                        else
                        {
                            await WriteResponse(tcpClient, streamWriter, "405 Method Not Allowed");
                            return;
                        }
                    }
                    else
                    {
                        //raw http header request
                        using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(headerParse.Uri, true);
                        using var remote_stream = sessionSource.GetStream();

                        using StreamWriter remote_writer = new StreamWriter(remote_stream);
                        await remote_writer.WriteLineAsync($"{headerParse.Method} {headerParse.Uri.AbsolutePath} HTTP/{headerParse.Version}").ConfigureAwait(false);
                        if (!lines.Any(x => x.Contains("host: ", StringComparison.OrdinalIgnoreCase)))
                            await remote_writer.WriteLineAsync($"Host: {headerParse.Uri.Host}").ConfigureAwait(false);
                        foreach (var line in lines.Skip(1)) await remote_writer.WriteLineAsync(line).ConfigureAwait(false);
                        await remote_writer.WriteLineAsync().ConfigureAwait(false);

                        await new StreamTransferHelper(networkStream, remote_stream).WaitUntilDisconnect().ConfigureAwait(false);
                    }

                    if (tcpClient.Connected)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                while (isKeepAlive);
            }
        }


        async Task WriteResponse(TcpClient tcpClient, StreamWriter streamWriter, string code_and_message, string content_message = null)
        {
            int contentLength = 0;
            byte[] content = null;
            if (!string.IsNullOrWhiteSpace(content_message))
            {
                content = Encoding.UTF8.GetBytes(content_message);
                contentLength = content.Length;
            }

#if DEBUG
            Console.WriteLine($"{tcpClient.Client.RemoteEndPoint} <= HTTP/1.1 {code_and_message}");
            Console.WriteLine($"{tcpClient.Client.RemoteEndPoint} <= Content-Length: {contentLength}");
#endif
            await streamWriter.WriteLineAsync($"HTTP/1.1 {code_and_message}").ConfigureAwait(false);
            await streamWriter.WriteLineAsync($"Content-Length: {contentLength}").ConfigureAwait(false);
            if (contentLength > 0 && content != null)
            {
                await streamWriter.WriteLineAsync($"Content-Type: text/html; charset=utf-8").ConfigureAwait(false);
            }
            await streamWriter.WriteLineAsync().ConfigureAwait(false);
            if (contentLength > 0 && content != null)
            {
                await streamWriter.BaseStream.WriteAsync(content, 0, contentLength).ConfigureAwait(false);
            }
            await streamWriter.FlushAsync().ConfigureAwait(false);
        }
    }
}
