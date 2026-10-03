namespace TqkLibrary.Proxy.Interfaces
{
    /// <summary>
    /// A stream decorator that can still tell the far end nothing more will be sent, the way
    /// <c>Socket.Shutdown(SocketShutdown.Send)</c> does for the <c>NetworkStream</c> it wraps. The
    /// relay looks for this when the stream it was handed is not a <c>NetworkStream</c> itself, so
    /// wrapping a tunnel does not quietly cost it its half-close.
    /// </summary>
    public interface IHalfClosableStream
    {
        /// <summary>Sends FIN on the underlying connection; the receiving direction stays open.</summary>
        void ShutdownSend();
    }
}
