using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Exceptions
{
    public class BindListenTimeoutException : Exception
    {
        public BindListenTimeoutException()
        {

        }
        public BindListenTimeoutException(string? message) : base(message)
        {

        }
    }
}
