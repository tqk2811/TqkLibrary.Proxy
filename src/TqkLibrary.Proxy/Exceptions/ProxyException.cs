namespace TqkLibrary.Proxy.Exceptions
{
    public abstract class ProxyException : Exception
    {
        protected ProxyException()
        {
        }
        protected ProxyException(string? message) : base(message)
        {
        }
        protected ProxyException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}
