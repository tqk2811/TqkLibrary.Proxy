using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TqkLibrary.Proxy.Reverse.Transport.WebSockets;

namespace TqkLibrary.Proxy.Reverse.Transport.AspNetCore
{
    public static class ReverseProxyEndpointRouteBuilderExtensions
    {
        /// <summary>
        /// Registers two WebSocket endpoints under <paramref name="prefix"/>:
        ///   {prefix}/control  and  {prefix}/data
        /// that feed WebSockets into the supplied <see cref="AspNetCoreReverseTransportServer"/>.
        /// The host's WebSocket middleware (<c>app.UseWebSockets()</c>) must be enabled.
        /// </summary>
        public static IEndpointRouteBuilder MapReverseProxy(
            this IEndpointRouteBuilder endpoints,
            string prefix,
            AspNetCoreReverseTransportServer transport)
        {
            if (string.IsNullOrEmpty(prefix)) throw new ArgumentException("prefix required", nameof(prefix));
            var ctrl = prefix.TrimEnd('/') + WebSocketTransportPaths.ControlPath;
            var data = prefix.TrimEnd('/') + WebSocketTransportPaths.DataPath;

            endpoints.Map(ctrl, async (HttpContext ctx) =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
                var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                var remote = ctx.Connection.RemoteIpAddress + ":" + ctx.Connection.RemotePort;
                await transport.AcceptControlAsync(ws, remote, ctx.RequestAborted).ConfigureAwait(false);
            });

            endpoints.Map(data, async (HttpContext ctx) =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
                var ws = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
                await transport.AcceptDataAsync(ws, ctx.RequestAborted).ConfigureAwait(false);
            });

            return endpoints;
        }
    }
}
