namespace TqkLibrary.Proxy.Vpn.WireProxyCli.Exceptions
{
    public class WireGuardException : Exception
    {
        public int? ExitCode { get; }
        public string? Stderr { get; }

        public WireGuardException(string message) : base(message) { }

        public WireGuardException(string message, int exitCode, string? stderr) : base(message)
        {
            ExitCode = exitCode;
            Stderr = stderr;
        }

        public WireGuardException(string message, Exception inner) : base(message, inner) { }
    }
}
