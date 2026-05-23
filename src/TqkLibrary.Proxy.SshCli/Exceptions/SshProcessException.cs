using TqkLibrary.Proxy.Exceptions;

namespace TqkLibrary.Proxy.SshCli.Exceptions
{
    public class SshProcessException : ProxySourceException
    {
        public int? ExitCode { get; }
        public string? StdError { get; }

        public SshProcessException()
        {
        }

        public SshProcessException(string? message) : base(message)
        {
        }

        public SshProcessException(string? message, int? exitCode, string? stdError)
            : base(message)
        {
            ExitCode = exitCode;
            StdError = stdError;
        }

        public SshProcessException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
