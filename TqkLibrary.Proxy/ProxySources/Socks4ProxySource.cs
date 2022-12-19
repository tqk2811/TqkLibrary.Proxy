using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxySources
{
    public class Socks4ProxySource : IProxySource, ISocks4Proxy
    {
        enum Socks4_CMD : byte
        {
            EstablishStreamConnection = 0x01,
            EstablishPortBinding = 0x02,
        }
        enum Socks4_REP : byte
        {
            RequestGranted = 0x5a,
            RequestRejectedOrFailed = 0x5b,
            RequestFailedBecauseClientIsNotRunningIdentd = 0x5c,
            RequestFailedBecauseClientIdentdCouldNotConfirmTheUserIdInTheRequest = 0x5d
        }


        const byte Socks4Version = 0x04;

        readonly IPEndPoint iPEndPoint;
        readonly string userId;
        public Socks4ProxySource(IPEndPoint iPEndPoint, string userId = null)
        {
            this.iPEndPoint = iPEndPoint ?? throw new ArgumentNullException(nameof(iPEndPoint));
            this.userId = userId ?? string.Empty;
        }

        public bool IsUseSocks4A { get; set; } = true;
        public bool IsSupportUdp => false;
        public bool IsSupportIpv6 => false;

        public async Task<ISessionSource> InitSessionAsync(Uri address)
        {
            if (address == null) throw new NullReferenceException(nameof(address));

            TcpClient tcpClient = new TcpClient();
            Stream networkStream = null;
            bool isSuccess = false;
            try
            {
                IPAddress iPAddress = null;
                switch (address.HostNameType)
                {
                    case UriHostNameType.IPv4:
                        IPAddress.TryParse(address.Host, out iPAddress);
                        break;

                    case UriHostNameType.Dns:
                        if (!IsUseSocks4A)
                        {
                            iPAddress = Dns.GetHostAddresses(address.Host).Where(x => x.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault();
                            if (iPAddress == null) throw new Exception($"{address.Host} not found");
                        }
                        break;

                    default: throw new NotSupportedException(nameof(address.HostNameType));
                }
                if (iPAddress == null)
                    throw new InvalidDataException();


                await tcpClient.ConnectAsync(iPEndPoint.Address, iPEndPoint.Port);
                networkStream = tcpClient.GetStream();


                bool isRequestDomain = iPAddress == null && IsUseSocks4A;
                //================first request====================
                UInt16 port = (UInt16)address.Port;
                byte[] ipv4_bytes = iPAddress.GetAddressBytes();
                byte[] req_buffer = new byte[]
                {
                        Socks4Version,
                        (byte)Socks4_CMD.EstablishStreamConnection,
                        (byte)(port >> 8),
                        (byte)(port),
                        isRequestDomain ? (byte)0 : ipv4_bytes[0],
                        isRequestDomain ? (byte)0 : ipv4_bytes[1],
                        isRequestDomain ? (byte)0 : ipv4_bytes[2],
                        isRequestDomain ? (byte)1 : ipv4_bytes[3],
                };
                await networkStream.WriteAsync(req_buffer, 0, req_buffer.Length);

                req_buffer = new byte[this.userId.Length + 1];
                req_buffer[this.userId.Length] = 0;
                if (!string.IsNullOrWhiteSpace(this.userId))
                {
                    Encoding.ASCII.GetBytes(this.userId, 0, this.userId.Length, req_buffer, 0);
                }
                await networkStream.WriteAsync(req_buffer, 0, req_buffer.Length);
                if (isRequestDomain)
                {
                    req_buffer = new byte[address.Host.Length + 1];
                    req_buffer[address.Host.Length] = 0;
                    Encoding.ASCII.GetBytes(address.Host, 0, address.Host.Length, req_buffer, 0);
                    await networkStream.WriteAsync(req_buffer, 0, req_buffer.Length);
                }
                await networkStream.FlushAsync();

                //================first reply====================
                byte[] res_buffer = await networkStream.ReadBytesAsync(8);
                switch ((Socks4_REP)res_buffer[1])
                {
                    case Socks4_REP.RequestGranted:
                        isSuccess = true;
                        return new TcpStreamSessionSource(tcpClient);

                    default:
                        throw new Exception(((Socks4_REP)res_buffer[1]).ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{nameof(Socks4ProxySource)}.{nameof(InitSessionAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
            }
            finally
            {
                if (!isSuccess)
                {
                    networkStream?.Dispose();
                    networkStream = null;
                    tcpClient?.Dispose();
                    tcpClient = null;
                }
            }
            return null;
        }
    }
}
