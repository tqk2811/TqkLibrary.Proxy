namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
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
}
