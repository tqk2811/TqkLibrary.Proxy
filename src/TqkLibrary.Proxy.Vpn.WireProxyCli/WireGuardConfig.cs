namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    public class WireGuardConfig
    {
        public WireGuardInterface Interface { get; set; } = new WireGuardInterface();
        public IList<WireGuardPeer> Peers { get; } = new List<WireGuardPeer>();
    }
}
