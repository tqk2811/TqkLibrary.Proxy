using Microsoft.Extensions.Logging;

namespace TqkLibrary.Proxy
{
    public static class Singleton
    {
        public static uint HeaderMaxLength { get; set; } = 40 * 1024;//40 KiB
        public static int ContentMaxLength { get; set; } = 2 * 1024 * 1024;//2 MiB
        public static ILoggerFactory? LoggerFactory { get; set; }
    }
}
