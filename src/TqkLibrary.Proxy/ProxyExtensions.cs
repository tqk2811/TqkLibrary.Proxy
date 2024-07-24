using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy
{
    public static class ProxyExtensions
    {
#if !NET5_0_OR_GREATER
        public static bool Contains(this string self, string value, StringComparison stringComparison)
        {
            return self.IndexOf(value, stringComparison) >= 0;
        }
        public static IEnumerable<T> Append<T>(this IEnumerable<T> values, params T[]? items)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            foreach (var item in values)
            {
                yield return item;
            }
            if (items is not null)
            {
                foreach (var item in items)
                {
                    yield return item;
                }
            }
        }
#endif
        public static byte[] GetByteArray(this IPacketData packetData) 
            => packetData.GetBytes().ToArray();
    }
}
