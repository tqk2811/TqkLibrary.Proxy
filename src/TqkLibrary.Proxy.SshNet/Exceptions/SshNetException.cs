using TqkLibrary.Proxy.Exceptions;

namespace TqkLibrary.Proxy.SshNet.Exceptions
{
    public class SshNetException : ProxySourceException
    {
        public SshNetException()
        {
        }

        public SshNetException(string? message) : base(message)
        {
        }

        public SshNetException(string? message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }
}
