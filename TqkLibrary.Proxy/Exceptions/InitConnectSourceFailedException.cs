using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Exceptions
{
    public class InitConnectSourceFailedException : Exception
    {
        public InitConnectSourceFailedException()
        {

        }
        public InitConnectSourceFailedException(string? message) : base(message)
        {

        }
    }
}
