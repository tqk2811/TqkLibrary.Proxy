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
                host: "192.168.1.5",
                user: "tqk2811"
                )
            {
                Password = "khanhmaple",
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
