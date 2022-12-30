using System.IO;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TqkLibrary.Proxy.Enums;
using TqkLibrary.Proxy.Interfaces;
using TqkLibrary.Proxy.ProxySources;
using TqkLibrary.Proxy.StreamHeplers;

namespace TqkLibrary.Proxy.ProxyServers
{
    public class Socks4ProxyServer : BaseProxyServer, ISocks4Proxy
    {
        public Socks4ProxyServer(IPEndPoint iPEndPoint, IProxySource proxySource) : base(iPEndPoint, proxySource)
        {

        }
        public bool IsUseSocks4A { get; set; } = true;

        protected override async Task ProxyWork(Stream stream, EndPoint remoteEndPoint)
        {
            /*  Socks4
             *              VER	    CMD	    DSTPORT     DSTIP   ID
             *  Byte Count	1	    1	    2	        4	    Variable
             *  -----------------------------------------------------
             *  Socks4a
             *              VER	    CMD	    DSTPORT     DSTIP   ID          DOMAIN
             *  Byte Count	1	    1	    2	        4	    Variable    variable
             *  
             *  in Socks4a, DSTIP willbe 0.0.0.x (x != 0)
             *  -----------------------------------------------------
             *  variable is string null (0x00) terminated
             */

            byte[] data_buffer = await stream.ReadBytesAsync(8);
            byte[] id = await stream.ReadUntilNullTerminated();
            byte[] host = null;
            if (data_buffer[4] == 0 && data_buffer[5] == 0 && data_buffer[6] == 0 && data_buffer[7] != 0)//sock4a
            {
                if (IsUseSocks4A)
                {
                    host = await stream.ReadUntilNullTerminated();
                }
                else return;//disconnect
            }

            //check auth id
            //if(failed)
            //{
            //    await WriteReplyAsync(stream, Socks4_REP.CouldNotConfirmTheUserId, remoteEndPoint);
            //    return;
            //}

            Socks4_CMD cmd = (Socks4_CMD)data_buffer[1];
            UInt16 port = BitConverter.ToUInt16(data_buffer, 2);
            IPAddress target_ip = null;
            if (host == null)
            {
                target_ip = new IPAddress(data_buffer.Skip(4).Take(4).ToArray());
            }
            else
            {
                string domain = Encoding.ASCII.GetString(host);
                if (string.IsNullOrWhiteSpace(domain))
                {
                    await WriteReplyAsync(stream, Socks4_REP.RequestRejectedOrFailed, remoteEndPoint);
                    return;
                }

                //ipv4 only because need to response
                target_ip = Dns.GetHostAddresses(domain).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                if (target_ip == null)
                {
                    await WriteReplyAsync(stream, Socks4_REP.RequestRejectedOrFailed, remoteEndPoint);
                    return;
                }
            }

            //release memory
            data_buffer = null;
            id = null;
            host = null;

            //connect to target
            switch (cmd)
            {
                case Socks4_CMD.EstablishStreamConnection:
                    await EstablishStreamConnection(stream, target_ip, port, remoteEndPoint);
                    return;

                case Socks4_CMD.EstablishPortBinding:
                    //not support now, write later
                    //it create listen port on this IProxySource and transfer with current connection
                    //and send reply ip:port listen
                    await WriteReplyAsync(stream, Socks4_REP.RequestRejectedOrFailed, remoteEndPoint);
                    return;

            }
        }

        async Task EstablishStreamConnection(Stream remote_stream, IPAddress target_ip, UInt16 target_port, EndPoint remoteEndPoint)
        {
            Uri uri = new Uri($"http://{target_ip}:{target_port}");
            IConnectionSource connectionSource = null;
            Stream session_stream = null;
            try
            {
                try
                {
                    connectionSource = await ProxySource.InitConnectionAsync(uri);
                    session_stream = connectionSource.GetStream();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks4ProxyServer)}.{nameof(EstablishStreamConnection)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                    await WriteReplyAsync(remote_stream, Socks4_REP.RequestRejectedOrFailed, remoteEndPoint);
                    return;
                }

                //send response to client
                await WriteReplyAsync(remote_stream, Socks4_REP.RequestGranted, remoteEndPoint);

                //transfer until disconnect
                await new StreamTransferHelper(remote_stream, session_stream)
#if DEBUG
                    .DebugName(remoteEndPoint.ToString(), uri.ToString())
#endif
                    .WaitUntilDisconnect();
            }
            finally
            {
                session_stream?.Dispose();
                connectionSource?.Dispose();
            }
        }



        Task WriteReplyAsync(
            Stream remote_stream,
            Socks4_REP rep,
            EndPoint remoteEndPoint,
            CancellationToken cancellationToken = default)
        {
            return WriteReplyAsync(remote_stream, rep, IPAddress.Any, 0, remoteEndPoint, cancellationToken);
        }

        async Task WriteReplyAsync(
            Stream remote_stream,
            Socks4_REP rep,
            IPAddress listen_ip,
            UInt16 listen_port,
            EndPoint remoteEndPoint,
            CancellationToken cancellationToken = default)
        {
            if (listen_ip.AddressFamily != AddressFamily.InterNetwork)
                throw new InvalidDataException($"{nameof(listen_ip)}.{nameof(AddressFamily)} must be {nameof(AddressFamily.InterNetwork)}");

            byte[] rep_buffer = new byte[8];
            rep_buffer[0] = 0;
            rep_buffer[1] = (byte)rep;
            rep_buffer[2] = (byte)(listen_port >> 8);
            rep_buffer[3] = (byte)listen_port;
            listen_ip.GetAddressBytes().CopyTo(rep_buffer, 4);
#if DEBUG
            Console.WriteLine($"[{nameof(Socks4ProxyServer)}.{nameof(WriteReplyAsync)}] {remoteEndPoint} << 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");
#endif
            await remote_stream.WriteAsync(rep_buffer, 0, rep_buffer.Length, cancellationToken);
            await remote_stream.FlushAsync(cancellationToken);
        }
    }
}
