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
#endif
    }
}
