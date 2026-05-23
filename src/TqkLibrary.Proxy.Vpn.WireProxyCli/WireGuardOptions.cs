using System.Net;

namespace TqkLibrary.Proxy.Vpn.WireProxyCli
{
    public class WireGuardOptions
    {
        /// <summary>
        /// Path to the wireproxy executable. If null, resolved from PATH (looks for "wireproxy"/"wireproxy.exe").
        /// </summary>
        public string? BinaryPath { get; set; }

        /// <summary>
        /// Inline WireGuard configuration. Mutually exclusive with <see cref="ConfigFilePath"/>.
        /// When set, a temp .conf file is generated for wireproxy.
        /// </summary>
        public WireGuardConfig? Config { get; set; }

        /// <summary>
        /// Path to an existing wireproxy-style .conf file. Mutually exclusive with <see cref="Config"/>.
        /// The file MUST already contain a [Socks5] section; the runner does not patch it.
        /// </summary>
        public string? ConfigFilePath { get; set; }

        /// <summary>
        /// SOCKS5 bind address used when generating config from <see cref="Config"/>.
        /// If null, binds 127.0.0.1 on a random free port.
        /// Ignored when <see cref="ConfigFilePath"/> is used.
        /// </summary>
        public IPEndPoint? Socks5BindAddress { get; set; }

        /// <summary>
        /// Optional SOCKS5 username for the local listener (generated config only).
        /// </summary>
        public string? Socks5Username { get; set; }

        /// <summary>
        /// Optional SOCKS5 password for the local listener (generated config only).
        /// </summary>
        public string? Socks5Password { get; set; }

        /// <summary>
        /// When <see cref="ConfigFilePath"/> is used, the caller must specify which local SOCKS5 endpoint
        /// is exposed by that config so the proxy source knows where to dial.
        /// </summary>
        public IPEndPoint? ExternalSocks5Endpoint { get; set; }

        /// <summary>
        /// Timeout (ms) waiting for wireproxy to come up and the SOCKS5 listener to accept connections.
        /// </summary>
        public int StartupTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// Whether the proxy source should report SOCKS5 UDP ASSOCIATE support.
        /// wireproxy's SOCKS5 implementation is TCP-only as of writing — leave false unless you know better.
        /// </summary>
        public bool IsSupportUdp { get; set; } = false;

        /// <summary>
        /// Whether the proxy source should report IPv6 support. WireGuard itself is dual-stack capable.
        /// </summary>
        public bool IsSupportIpv6 { get; set; } = true;

        /// <summary>
        /// When true (default), <see cref="WireGuardProxySource"/> transparently respawns
        /// wireproxy on the next call if the previous subprocess has exited.
        /// </summary>
        public bool AutoRestart { get; set; } = true;

        /// <summary>
        /// Extra args appended to the wireproxy invocation.
        /// </summary>
        public IList<string> ExtraArgs { get; } = new List<string>();
    }
}
