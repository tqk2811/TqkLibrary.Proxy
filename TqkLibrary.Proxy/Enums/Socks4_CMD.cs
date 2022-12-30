namespace TqkLibrary.Proxy.ProxySources
{
    internal enum Socks4_CMD : byte
    {
        EstablishStreamConnection = 0x01,
        EstablishPortBinding = 0x02,
    }
}
