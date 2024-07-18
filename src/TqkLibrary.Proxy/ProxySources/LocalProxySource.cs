using System.Net;
using System.Net.Sockets;
using TqkLibrary.Proxy.Interfaces;

namespace TqkLibrary.Proxy.ProxySources
{
    public partial class LocalProxySource : IProxySource, IHttpProxy
    {
        public virtual bool IsSupportUdp { get; set; } = true;
        public virtual bool IsSupportIpv6 { get; set; } = true;
        public virtual bool IsSupportBind { get; set; } = true;
        /// <summary>
        /// window only
        /// </summary>
        public virtual bool IsAllowNatTraversal { get; set; } = false;
        public virtual int BindListenTimeout { get; set; } = 30000;

        public virtual IConnectSource GetConnectSource(Guid tunnelId)
        {
            return new ConnectTunnel(this, tunnelId);
        }

        public virtual IBindSource GetBindSource(Guid tunnelId)
        {
            return new BindTunnel(this, tunnelId);
        }

        public virtual IUdpAssociateSource GetUdpAssociateSource(Guid tunnelId)
        {
            throw new NotSupportedException();
            //return new UdpTunnel(this);
        }

        public virtual Task<IPEndPoint> GetListenEndPointAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new IPEndPoint(IPAddress.Any, 0));
        }
        public virtual Task<IPAddress> GetResponseIPAddressAsync(CancellationToken cancellationToken = default)
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
