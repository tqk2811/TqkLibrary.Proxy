namespace TqkLibrary.Proxy.Exceptions
{
    public abstract class ProxySourceException : ProxyException
    {
        protected ProxySourceException()
        {
        }
        protected ProxySourceException(string? message) : base(message)
        {
        }
        protected ProxySourceException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
