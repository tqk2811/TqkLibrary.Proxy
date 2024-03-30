namespace TqkLibrary.Proxy.Enums
{
    internal enum Socks5_CMD : byte
    {
        EstablishStreamConnection = 0x01,
        EstablishPortBinding = 0x02,
        AssociateUDP = 0x03
    }
}
