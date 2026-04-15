using TestProxy.ServerTest;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.SshCli;

namespace TestProxy.Local
{
    [TestClass]
    public class OpenSshProxySourceTest : HttpProxyServerTest
    {
        private OpenSshProxySource? _sshProxySource;

        protected override IProxySource GetProxySource()
        {
            var options = new OpenSshConnectionOptions(
                host: "192.168.1.5",
                user: "tqk2811"
                )
            {
                Password = "khanhmaple",
            };
            _sshProxySource = new OpenSshProxySource(options);
            return _sshProxySource;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            _sshProxySource?.Dispose();
        }
    }
}
