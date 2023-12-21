using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy
{
    internal static class Extensions
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
        internal static IEnumerable<T> Except<T>(this IEnumerable<T> source, params T[] values)
            => source.Except(values.AsEnumerable());
        internal static IEnumerable<T> Concat<T>(this IEnumerable<T> source, params T[] values)
            => source.Concat(values.AsEnumerable());
        internal static IEnumerable<T> ConcatIf<T>(this IEnumerable<T> source, bool val, params T[] values)
            => val ? source.Concat(values.AsEnumerable()) : source;
    }
}
