namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    public class WireGuardConfig
    {
        public WireGuardInterface Interface { get; set; } = new WireGuardInterface();
        public IList<WireGuardPeer> Peers { get; } = new List<WireGuardPeer>();
    }

    public class WireGuardInterface
    {
        public string? PrivateKey { get; set; }

        /// <summary>
        /// VPN LAN address(es) assigned to this client. Can be one or many CIDRs (e.g. "10.0.0.2/32", "fd00::2/128").
        /// </summary>
        public IList<string> Address { get; } = new List<string>();

        /// <summary>
        /// DNS server(s) to use inside the VPN. Optional.
        /// </summary>
        public IList<string> DNS { get; } = new List<string>();

        public int? MTU { get; set; }
    }

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
