namespace TqkLibrary.Proxy.Handlers
{
    public class BaseProxySourceHandler
    {
        BaseProxySourceHandler? _parent = null;
        public BaseProxySourceHandler()
        {

        }
        public BaseProxySourceHandler(BaseProxySourceHandler parent)
        {
            this._parent = parent ?? throw new ArgumentNullException(nameof(parent));
        }
    }
}
