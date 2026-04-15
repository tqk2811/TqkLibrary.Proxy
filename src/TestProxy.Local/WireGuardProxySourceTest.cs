using TestProxy.ServerTest;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.Vpn.WireProxyCli;

namespace TestProxy.Local
{
    [TestClass]
    public class WireGuardProxySourceTest : HttpProxyServerTest
    {
        // Set these to run the test against a real WireGuard endpoint + wireproxy binary.
        // Leave null/empty to skip.
        private const string? WireProxyBinaryPath = null;
        private const string? PrivateKey = null;
        private const string? PeerPublicKey = null;
        private const string? PeerEndpoint = null;
        private const string ClientAddressCidr = "10.0.0.2/32";

        private WireGuardProxySource? _wgProxySource;

        protected override IProxySource GetProxySource()
        {
            if (string.IsNullOrEmpty(WireProxyBinaryPath) ||
                string.IsNullOrEmpty(PrivateKey) ||
                string.IsNullOrEmpty(PeerPublicKey) ||
                string.IsNullOrEmpty(PeerEndpoint))
            {
                Assert.Inconclusive("WireGuard test config not provided.");
            }

            var options = new WireGuardOptions
            {
                BinaryPath = WireProxyBinaryPath,
                Config = new WireGuardConfig
                {
                    Interface = new WireGuardInterface { PrivateKey = PrivateKey },
                    Peers =
                    {
                        new WireGuardPeer
                        {
                            PublicKey = PeerPublicKey,
                            Endpoint = PeerEndpoint,
                            AllowedIPs = { "0.0.0.0/0" },
                            PersistentKeepalive = 25,
                        },
                    },
                },
            };
            options.Config!.Interface.Address.Add(ClientAddressCidr);

            _wgProxySource = new WireGuardProxySource(options);
            return _wgProxySource;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            _wgProxySource?.Dispose();
        }
    }
}
