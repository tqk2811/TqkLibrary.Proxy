using System.Globalization;
using System.Net;
using System.Text;
using TqkLibrary.Proxy.Vpn.WireProxyCli.Exceptions;

namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    /// <summary>
    /// Builds a wireproxy-flavoured WireGuard config (standard [Interface]/[Peer] sections plus
    /// the wireproxy-specific [Socks5] section). Spec: https://github.com/pufferffish/wireproxy
    /// </summary>
    internal static class WireGuardConfigWriter
    {
        /// <param name="defaultPersistentKeepalive">
        /// Written for peers that carry no PersistentKeepalive of their own; null writes none.
        /// See <see cref="WireGuardOptions.DefaultPersistentKeepalive"/> for why it matters.
        /// </param>
        public static string Build(
            WireGuardConfig config, IPEndPoint socks5Bind, string? socks5User, string? socks5Pass,
            int? defaultPersistentKeepalive = null)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            if (config.Interface is null) throw new WireGuardException("WireGuardConfig.Interface is required.");
            if (string.IsNullOrWhiteSpace(config.Interface.PrivateKey))
                throw new WireGuardException("Interface.PrivateKey is required.");
            if (config.Interface.Address.Count == 0)
                throw new WireGuardException("Interface.Address must contain at least one CIDR.");
            if (config.Peers.Count == 0)
                throw new WireGuardException("At least one Peer is required.");

            var sb = new StringBuilder();
            sb.AppendLine("[Interface]");
            sb.Append("PrivateKey = ").AppendLine(config.Interface.PrivateKey);
            sb.Append("Address = ").AppendLine(string.Join(", ", config.Interface.Address));
            if (config.Interface.DNS.Count > 0)
                sb.Append("DNS = ").AppendLine(string.Join(", ", config.Interface.DNS));
            if (config.Interface.MTU.HasValue)
                sb.Append("MTU = ").AppendLine(config.Interface.MTU.Value.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine();

            for (int i = 0; i < config.Peers.Count; i++)
            {
                var peer = config.Peers[i];
                if (string.IsNullOrWhiteSpace(peer.PublicKey))
                    throw new WireGuardException($"Peer[{i}].PublicKey is required.");
                if (string.IsNullOrWhiteSpace(peer.Endpoint))
                    throw new WireGuardException($"Peer[{i}].Endpoint is required.");
                if (peer.AllowedIPs.Count == 0)
                    throw new WireGuardException($"Peer[{i}].AllowedIPs must contain at least one CIDR.");

                sb.AppendLine("[Peer]");
                sb.Append("PublicKey = ").AppendLine(peer.PublicKey);
                if (!string.IsNullOrWhiteSpace(peer.PresharedKey))
                    sb.Append("PresharedKey = ").AppendLine(peer.PresharedKey);
                sb.Append("Endpoint = ").AppendLine(peer.Endpoint);
                sb.Append("AllowedIPs = ").AppendLine(string.Join(", ", peer.AllowedIPs));
                int? keepalive = peer.PersistentKeepalive ?? defaultPersistentKeepalive;
                if (keepalive.HasValue)
                    sb.Append("PersistentKeepalive = ").AppendLine(keepalive.Value.ToString(CultureInfo.InvariantCulture));
                sb.AppendLine();
            }

            sb.AppendLine("[Socks5]");
            sb.Append("BindAddress = ").AppendLine(socks5Bind.ToString());
            if (!string.IsNullOrEmpty(socks5User)) sb.Append("Username = ").AppendLine(socks5User);
            if (!string.IsNullOrEmpty(socks5Pass)) sb.Append("Password = ").AppendLine(socks5Pass);

            return sb.ToString();
        }
    }
}
