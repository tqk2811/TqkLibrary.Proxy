using Microsoft.Extensions.Logging;

namespace TqkLibrary.Proxy
{
    public abstract class BaseLogger
    {
        protected readonly ILogger? _logger;
        public BaseLogger()
        {
            _logger = Singleton.LoggerFactory?.CreateLogger(GetType());
        }
    }
}
