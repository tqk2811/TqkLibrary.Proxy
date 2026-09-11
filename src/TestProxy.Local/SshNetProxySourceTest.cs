using TestProxy.ServerTest;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshNet;

namespace TestProxy.Local
{
    [TestClass]
    public class SshNetProxySourceTest : HttpProxyServerTest
    {
        private SshNetProxySource? _sshProxySource;

        protected override IProxySource GetProxySource()
        {
            var options = new SshNetConnectionOptions(
                host: "192.168.1.7",
                user: "tqk2811"
                )
            {
                Password = "khanhmaple",
                // A server on the local network, set up for this test: nothing here would know its key.
                AcceptAnyHostKey = true,
            };
            _sshProxySource = new SshNetProxySource(options);
            return _sshProxySource;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            _sshProxySource?.Dispose();
        }
    }
}
