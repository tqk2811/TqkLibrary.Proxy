using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.Handlers
{
    public class LocalProxySourceHandler : BaseProxySourceHandler
    {
        readonly LocalProxySourceHandler? _parent = null;
        public LocalProxySourceHandler()
        {

        }

        public LocalProxySourceHandler(LocalProxySourceHandler parent) : base(parent)
        {
            _parent = parent;
        }

        public virtual Task<IPEndPoint> GetListenEndPointAsync(CancellationToken cancellationToken = default)
        {
            if (_parent is not null) return _parent.GetListenEndPointAsync(cancellationToken);
            else return Task.FromResult(new IPEndPoint(IPAddress.Any, 0));
        }

        public virtual Task<IPAddress> GetResponseIPAddressAsync(CancellationToken cancellationToken = default)
        {
            if (_parent is not null)
            {
                return _parent.GetResponseIPAddressAsync(cancellationToken);
            }
            else
            {
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    socket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = (IPEndPoint)socket.LocalEndPoint;
                    return Task.FromResult(endPoint.Address);
                }
            }
        }
    }
}
