namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    public class WireGuardPeer
    {
        public string? PublicKey { get; set; }
        public string? PresharedKey { get; set; }

        /// <summary>
        /// Server endpoint, e.g. "vpn.example.com:51820".
        /// </summary>
        public string? Endpoint { get; set; }

        /// <summary>
        /// AllowedIPs CIDR list. Use "0.0.0.0/0" to route all IPv4 through the peer.
        /// </summary>
        public IList<string> AllowedIPs { get; } = new List<string>();

        public int? PersistentKeepalive { get; set; }
    }
}
