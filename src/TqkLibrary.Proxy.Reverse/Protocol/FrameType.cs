namespace TqkLibrary.Proxy.Reverse.Protocol
{
    public enum FrameType : byte
    {
        Hello = 1,
        HelloAck = 2,
        HelloDeny = 3,

        OpenConnect = 10,
        OpenBind = 11,
        OpenUdp = 12,

        TunnelOpened = 20,
        TunnelError = 21,
        TunnelClose = 22,

        BindReady = 30,
        BindAccepted = 31,

        DataHello = 40,

        Ping = 90,
        Pong = 91,
    }
}
