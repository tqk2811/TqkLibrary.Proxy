using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using TqkLibrary.Proxy.Reverse.Transport;

namespace TqkLibrary.Proxy.Reverse.Transport.WebSockets
{
    public sealed class WebSocketReverseTransportClient : IReverseTransportClient
    {
        private readonly Uri _baseUri;
        private readonly string _controlPath;
        private readonly string _dataPath;

        public X509CertificateCollection? ClientCertificates { get; set; }
        public Action<ClientWebSocketOptions>? ConfigureSocket { get; set; }

        /// <param name="baseUri">e.g. ws://host:9000 or wss://host:9000</param>
        public WebSocketReverseTransportClient(Uri baseUri,
            string controlPath = WebSocketTransportPaths.ControlPath,
            string dataPath = WebSocketTransportPaths.DataPath)
        {
            _baseUri = baseUri;
            _controlPath = controlPath;
            _dataPath = dataPath;
        }

        public async Task<IControlChannel> ConnectControlAsync(CancellationToken cancellationToken = default)
        {
            var ws = await DialAsync(_controlPath, cancellationToken).ConfigureAwait(false);
            return new StreamControlChannel(new WebSocketStream(ws), _baseUri.Authority);
        }

        public async Task<Stream> OpenDataAsync(CancellationToken cancellationToken = default)
        {
            var ws = await DialAsync(_dataPath, cancellationToken).ConfigureAwait(false);
            return new WebSocketStream(ws);
        }

        private async Task<ClientWebSocket> DialAsync(string path, CancellationToken ct)
        {
            var ws = new ClientWebSocket();
            if (ClientCertificates is not null) ws.Options.ClientCertificates = ClientCertificates;
            ConfigureSocket?.Invoke(ws.Options);
            var uri = new Uri(_baseUri, path);
            try
            {
                await ws.ConnectAsync(uri, ct).ConfigureAwait(false);
                return ws;
            }
            catch
            {
                ws.Dispose();
                throw;
            }
        }
    }
}
