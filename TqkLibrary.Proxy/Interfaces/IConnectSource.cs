using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Interfaces
{
    public interface IConnectSource : IDisposable
    {
        Stream GetStream();
    }
}
