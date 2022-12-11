using System.Net;
using System.Net.Sockets;
using System.Net.Http.Headers;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using System.Text.RegularExpressions;
using System.Net.Http;
using TqkLibrary.Proxy.StreamHeplers;

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

        protected override async Task ProxyWork(TcpClient client_TcpClient)
        {
            using (client_TcpClient)
            {
                using NetworkStream client_NetworkStream = client_TcpClient.GetStream();
                using StreamHeaderReader client_StreamReader = new StreamHeaderReader(client_NetworkStream);
                using StreamHeaderWriter client_StreamWriter = new StreamHeaderWriter(client_NetworkStream);

                bool client_isKeepAlive = false;
                do
                {
                    List<string> client_HeaderLines = await client_StreamReader.ReadHeader();
#if DEBUG
                    client_HeaderLines.ForEach(x => Console.WriteLine($"{client_TcpClient.Client.RemoteEndPoint} => {x}"));
#endif
                    HeaderParse client_HeaderParse = client_HeaderLines.Parse();

                    //Check Proxy-Authorization
                    if (Credentials != null)
                    {
                        if (client_HeaderParse.ProxyAuthorization == null)
                        {
                            await WriteResponse(client_TcpClient, client_StreamWriter, "407 Proxy Authentication Required");
                            return;
                        }
                        else
                        {

                        }
                    }

                    client_isKeepAlive = client_HeaderParse.IsKeepAlive;

                    if ("CONNECT".Equals(client_HeaderParse.Method, StringComparison.OrdinalIgnoreCase))
                    {
                        //https proxy
                        if (AllowHttps)
                        {
                            using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(client_HeaderParse.Uri, true);
                            if (sessionSource == null)
                            {
                                await WriteResponse(client_TcpClient, client_StreamWriter, "408 Request Timeout");
                                return;
                            }
                            else
                            {
                                await WriteResponse(client_TcpClient, client_StreamWriter, "200 Connection established");
                            }

                            using var remote_stream = sessionSource.GetStream();
                            await new StreamTransferHelper(client_NetworkStream, remote_stream).WaitUntilDisconnect().ConfigureAwait(false);
                        }
                        else
                        {
                            await WriteResponse(client_TcpClient, client_StreamWriter, "405 Method Not Allowed");
                            return;
                        }
                    }
                    else
                    {
                        //raw http header request
                        using ISessionSource sessionSource = await this.ProxySource.InitSessionAsync(client_HeaderParse.Uri, true);
                        using Stream target_Stream = sessionSource.GetStream();

                        using StreamHeaderWriter target_StreamWriter = new StreamHeaderWriter(target_Stream);
                        using StreamHeaderReader target_StreamReader = new StreamHeaderReader(target_Stream);

                        //send header to target
                        {
                            List<string> headerLines = new List<string>();
                            headerLines.Add($"{client_HeaderParse.Method} {client_HeaderParse.Uri.AbsolutePath} HTTP/{client_HeaderParse.Version}");
                            if (!client_HeaderLines.Any(x => x.StartsWith("host: ", StringComparison.OrdinalIgnoreCase)))
                            {
                                headerLines.Add($"Host: {client_HeaderParse.Uri.Host}");
                            }
                            foreach (var line in client_HeaderLines.Skip(1)
                                .Where(x => !x.StartsWith("Proxy-Authorization: ", StringComparison.OrdinalIgnoreCase)))
                            {
                                headerLines.Add(line);
                            }

                            foreach (var line in headerLines)
                            {
                                await target_StreamWriter.WriteLineAsync(line).ConfigureAwait(false);
#if DEBUG
                                Console.WriteLine($"{client_HeaderParse.Uri.Host} << {line}");
#endif
                            }
                            await target_StreamWriter.WriteLineAsync().ConfigureAwait(false);
                            //await target_StreamWriter.FlushAsync().ConfigureAwait(false);
                        }

                        //Transfer content from client to target if have
                        await client_NetworkStream.TransferAsync(target_Stream, client_HeaderParse.ContentLength).ConfigureAwait(false);
                        await target_Stream.FlushAsync().ConfigureAwait(false);


                        //-----------------------------------------------------
                        //read header from target, and send back to client
                        List<string> target_response_HeaderLines = await target_StreamReader.ReadHeader();
                        int ContentLength = target_response_HeaderLines.GetContentLength();
                        foreach (var line in target_response_HeaderLines)
                        {
                            await client_StreamWriter.WriteLineAsync(line).ConfigureAwait(false);
#if DEBUG
                            Console.WriteLine($"{client_HeaderParse.Uri.Host} >> {line}");
#endif
                        }
                        await client_StreamWriter.WriteLineAsync().ConfigureAwait(false);

                        //Transfer content from target to client if have
                        await target_Stream.TransferAsync(client_NetworkStream, ContentLength).ConfigureAwait(false);
                        await client_NetworkStream.FlushAsync().ConfigureAwait(false);
                    }

                    if (client_TcpClient.Connected)
                    {
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                while (client_isKeepAlive);
            }
        }


        async Task WriteResponse(TcpClient tcpClient, StreamHeaderWriter streamHeaderWriter, string code_and_message, string content_message = null)
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
            await streamHeaderWriter.WriteLineAsync($"HTTP/1.1 {code_and_message}").ConfigureAwait(false);
            await streamHeaderWriter.WriteLineAsync($"Content-Length: {contentLength}").ConfigureAwait(false);
            if (contentLength > 0 && content != null)
            {
                await streamHeaderWriter.WriteLineAsync($"Content-Type: text/html; charset=utf-8").ConfigureAwait(false);
            }
            await streamHeaderWriter.WriteLineAsync().ConfigureAwait(false);
            if (contentLength > 0 && content != null)
            {
                await streamHeaderWriter.BaseStream.WriteAsync(content, 0, contentLength).ConfigureAwait(false);
            }
            await streamHeaderWriter.FlushAsync().ConfigureAwait(false);
        }
    }
}
