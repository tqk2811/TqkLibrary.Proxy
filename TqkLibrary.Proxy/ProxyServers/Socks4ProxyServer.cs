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

        protected override async Task ProxyWorkAsync(Stream client_stream, EndPoint client_EndPoint, CancellationToken cancellationToken = default)
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

            byte[] data_buffer = await client_stream.ReadBytesAsync(8, cancellationToken);
            byte[] id = await client_stream.ReadUntilNullTerminated(cancellationToken: cancellationToken);
            byte[] host = null;
            bool isSocks4A = false;
            if (data_buffer[4] == 0 && data_buffer[5] == 0 && data_buffer[6] == 0 && data_buffer[7] != 0)//socks4a
            {
                isSocks4A = true;
                if (IsUseSocks4A)
                {
                    host = await client_stream.ReadUntilNullTerminated(cancellationToken: cancellationToken);
                }
                else return;//disconnect
            }
            if (isSocks4A && host == null)//when IsUseSocks4A is false
            {
                await WriteReplyAsync(client_stream, Socks4_REP.RequestRejectedOrFailed, client_EndPoint, cancellationToken);
                return;
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
                    await WriteReplyAsync(client_stream, Socks4_REP.RequestRejectedOrFailed, client_EndPoint, cancellationToken);
                    return;
                }

                //ipv4 only because need to response
                target_ip = Dns.GetHostAddresses(domain).FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                if (target_ip == null)
                {
                    await WriteReplyAsync(client_stream, Socks4_REP.RequestRejectedOrFailed, client_EndPoint, cancellationToken);
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
                    await EstablishStreamConnectionAsync(client_stream, target_ip, port, client_EndPoint, cancellationToken);
                    return;

                case Socks4_CMD.EstablishPortBinding:
                    if (ProxySource.IsSupportBind)
                    {
                        //not support now, write later
                        //it create listen port on this IProxySource and transfer with current connection
                        //and send reply ip:port listen
                        await WriteReplyAsync(client_stream, Socks4_REP.RequestRejectedOrFailed, client_EndPoint, cancellationToken);
                    }
                    else
                    {
                        //not support
                        await WriteReplyAsync(client_stream, Socks4_REP.RequestRejectedOrFailed, client_EndPoint, cancellationToken);
                    }
                    return;

            }
        }

        async Task EstablishStreamConnectionAsync(
            Stream client_stream,
            IPAddress target_ip,
            UInt16 target_port,
            EndPoint client_EndPoint,
            CancellationToken cancellationToken = default)
        {
            Uri uri = new Uri($"http://{target_ip}:{target_port}");
            IConnectionSource connectionSource = null;
            Stream session_stream = null;
            try
            {
                try
                {
                    connectionSource = await ProxySource.InitConnectionAsync(uri, cancellationToken);
                    session_stream = connectionSource.GetStream();
                }
                catch (Exception ex)
                {
#if DEBUG
                    Console.WriteLine($"[{nameof(Socks4ProxyServer)}.{nameof(EstablishStreamConnectionAsync)}] {ex.GetType().FullName}: {ex.Message}, {ex.StackTrace}");
#endif
                    await WriteReplyAsync(client_stream, Socks4_REP.RequestRejectedOrFailed, client_EndPoint, cancellationToken);
                    return;
                }

                //send response to client
                await WriteReplyAsync(client_stream, Socks4_REP.RequestGranted, client_EndPoint, cancellationToken);

                //transfer until disconnect
                await new StreamTransferHelper(client_stream, session_stream)
#if DEBUG
                    .DebugName(client_EndPoint.ToString(), uri.ToString())
#endif
                    .WaitUntilDisconnect(cancellationToken);
            }
            finally
            {
                session_stream?.Dispose();
                connectionSource?.Dispose();
            }
        }



        Task WriteReplyAsync(
            Stream client_stream,
            Socks4_REP rep,
            EndPoint client_EndPoint,
            CancellationToken cancellationToken = default)
        {
            return WriteReplyAsync(client_stream, rep, IPAddress.Any, 0, client_EndPoint, cancellationToken);
        }

        async Task WriteReplyAsync(
            Stream client_stream,
            Socks4_REP rep,
            IPAddress listen_ip,
            UInt16 listen_port,
            EndPoint client_EndPoint,
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
            Console.WriteLine($"[{nameof(Socks4ProxyServer)}.{nameof(WriteReplyAsync)}] {client_EndPoint} << 0x{BitConverter.ToString(rep_buffer).Replace("-", "")}");
#endif
            await client_stream.WriteAsync(rep_buffer, cancellationToken);
            await client_stream.FlushAsync(cancellationToken);
        }
    }
}
