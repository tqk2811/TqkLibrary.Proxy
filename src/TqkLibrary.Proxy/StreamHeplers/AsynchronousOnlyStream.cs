namespace TqkLibrary.Proxy.StreamHeplers
{
    public class AsynchronousOnlyStream : BaseInheritStream
    {
        public AsynchronousOnlyStream(Stream baseStream, bool disposeBaseStream = true) : base(baseStream, disposeBaseStream)
        {

        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException($"Use asynchronous method only");
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException($"Use asynchronous method only");
        }
    }
}
