using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using TqkLibrary.Proxy.Authentications;

namespace TqkLibrary.Proxy.Filters
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseProxyServerFilter
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tcpClient"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<bool> IsAcceptClientFilterAsync(TcpClient tcpClient, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// SSL or encrypt ......
        /// </summary>
        /// <param name="networkStream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual Task<Stream> StreamFilterAsync(Stream networkStream, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(networkStream);
        }
    }
}
