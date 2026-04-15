namespace TqkLibrary.Proxy.SshCli
{
    public class OpenSshConnectionOptions
    {
        public string Host { get; }
        public int Port { get; }
        public string User { get; }

        public string? IdentityFile { get; set; }

        /// <summary>
        /// Password authentication (Linux/macOS only, requires sshpass).
        /// Not supported on Windows.
        /// </summary>
        public string? Password { get; set; }

        /// <summary>
        /// Path to ssh executable. If null, resolved from PATH.
        /// </summary>
        public string? SshExecutablePath { get; set; }

        /// <summary>
        /// Path to sshpass executable (Linux/macOS only). If null, resolved from PATH when Password is set.
        /// </summary>
        public string? SshPassExecutablePath { get; set; }

        /// <summary>
        /// Reuse one SSH session for multiple connections via ControlMaster.
        /// </summary>
        public bool UseControlMaster { get; set; } = true;

        /// <summary>
        /// StrictHostKeyChecking value. Default "accept-new".
        /// </summary>
        public string StrictHostKeyChecking { get; set; } = "accept-new";

        /// <summary>
        /// Path to known_hosts file. If null, uses ssh default.
        /// </summary>
        public string? UserKnownHostsFile { get; set; }

        /// <summary>
        /// ServerAliveInterval seconds. 0 disables.
        /// </summary>
        public int ServerAliveInterval { get; set; } = 30;

        /// <summary>
        /// ControlPersist seconds for master connection.
        /// </summary>
        public int ControlPersistSeconds { get; set; } = 60;

        /// <summary>
        /// Extra args appended to every ssh invocation.
        /// </summary>
        public IList<string> ExtraArgs { get; } = new List<string>();

        /// <summary>
        /// Timeout (ms) waiting for ssh to establish the stdio tunnel before assuming failure.
        /// </summary>
        public int ConnectProbeTimeoutMs { get; set; } = 5000;

        public OpenSshConnectionOptions(string host, string user, int port = 22)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("host required", nameof(host));
            if (string.IsNullOrWhiteSpace(user)) throw new ArgumentException("user required", nameof(user));
            if (port <= 0 || port > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(port));
            Host = host;
            User = user;
            Port = port;
        }
    }
}
