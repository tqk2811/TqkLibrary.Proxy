using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices;
using TqkLibrary.Proxy.GlobalUnicast.Structs;
using System.Threading.Tasks;

namespace TqkLibrary.Proxy.GlobalUnicast
{
    internal static class SlaccHelper
    {
        static IEnumerable<NetworkInterface> GetNetworkInterfaces()
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;

                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.Description.ToLower().Contains("virtual") ||
                    nic.Description.ToLower().Contains("vmware") ||
                    nic.Description.ToLower().Contains("pseudo") ||
                    nic.Description.ToLower().Contains("bluetooth"))
                    continue;

                yield return nic;
            }
        }

        public static IPAddress? FindGlobalUnicastPrefix()
        {
            foreach (var nic in GetNetworkInterfaces())
            {
                var props = nic.GetIPProperties();
                foreach (var unicast in props.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetworkV6 &&
                        unicast.Address.IsIPv6LinkLocal == false &&
                        !unicast.Address.IsIPv6Multicast &&
                        !IPAddress.IsLoopback(unicast.Address) &&
                        unicast.PrefixOrigin == PrefixOrigin.RouterAdvertisement)
                    {
                        byte[] fullBytes = unicast.Address.GetAddressBytes();
                        byte[] prefix = new byte[16];
                        Array.Copy(fullBytes, 0, prefix, 0, 8); // giữ nguyên 64-bit đầu
                                                                // zero phần interface ID
                        for (int i = 8; i < 16; i++) prefix[i] = 0;
                        return new IPAddress(prefix);
                    }
                }
            }
            return null;
        }
        public static IPAddress GenerateSlaacAddress(IPAddress prefix)
        {
            if (prefix.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                throw new ArgumentException("Only IPv6 supported");

            // Generate random interface ID (64-bit)
            byte[] suffix = new byte[8];
            new Random().NextBytes(suffix);

            // Combine prefix + suffix
            byte[] full = prefix.GetAddressBytes();
            Array.Resize(ref full, 16);
            Array.Copy(suffix, 0, full, 8, 8);

            return new IPAddress(full);
        }

        public static async Task AssignIPv6ToFirstUpInterface(IPAddress ipv6Address, byte prefixLength = 64)
        {
            var ip = ipv6Address;
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                throw new ArgumentException("Only IPv6 supported");

            var firstUpNic = GetNetworkInterfaces().FirstOrDefault();
            if (firstUpNic == null)
            {
                throw new InvalidOperationException("No active network interface found.");
            }

            int ifIndex = GetInterfaceIndex(firstUpNic);
            if (ifIndex == 0)
            {
                throw new InvalidOperationException("Unable to get interface index.");
            }

            SOCKADDR_INET sockaddr = new SOCKADDR_INET
            {
                si_family = 23, // AF_INET6
                si_data = new byte[28]
            };

            byte[] rawAddress = ip.GetAddressBytes();
            Array.Copy(rawAddress, 0, sockaddr.si_data, 6, 16); // IPv6 starts at offset 8

            NativeWrapper.InitializeUnicastIpAddressEntry(out var row);
            row.Address = sockaddr;
            row.InterfaceIndex = (uint)ifIndex;
            row.OnLinkPrefixLength = prefixLength;
            row.PrefixOrigin = 3; // IpPrefixOriginRouterAdvertisement
            row.SuffixOrigin = 4; // IpSuffixOriginRandom
            row.ValidLifetime = 0xFFFFFFFF; // infinite
            row.PreferredLifetime = 0xFFFFFFFF; // infinite
            row.SkipAsSource = false;
            row.DadState = 0;     // IpDadStatePreferred

            int result = NativeWrapper.CreateUnicastIpAddressEntry(ref row);

            if (result == 0)
            {
                Console.WriteLine($"Successfully assigned {ipv6Address} to {firstUpNic.Name}");
                await Task.Delay(3000);
            }
            else
            {
                throw new InvalidOperationException($"Failed with error code {result}, LastError: {Marshal.GetLastWin32Error()}");
            }
        }

        public static bool RemoveIPv6FromFirstUpInterface(IPAddress ipv6Address)
        {
            var ip = ipv6Address;
            if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
                throw new ArgumentException("Only IPv6 supported");

            var firstUpNic = NetworkInterface.GetAllNetworkInterfaces()
                .FirstOrDefault(nic => nic.OperationalStatus == OperationalStatus.Up);

            if (firstUpNic == null)
            {
                Console.WriteLine("No active network interface found.");
                return false;
            }

            int ifIndex = GetInterfaceIndex(firstUpNic);
            if (ifIndex == 0)
            {
                Console.WriteLine("Unable to get interface index.");
                return false;
            }

            SOCKADDR_INET sockaddr = new SOCKADDR_INET
            {
                si_family = 23, // AF_INET6
                si_data = new byte[28]
            };

            byte[] rawAddress = ip.GetAddressBytes();
            Array.Copy(rawAddress, 0, sockaddr.si_data, 8, 16); // IPv6 starts at offset 8

            NativeWrapper.InitializeUnicastIpAddressEntry(out var row);
            row.Address = sockaddr;
            row.InterfaceIndex = (uint)ifIndex;

            int result = NativeWrapper.DeleteUnicastIpAddressEntry(ref row);

            if (result == 0)
            {
                Console.WriteLine($"Successfully removed {ipv6Address} from {firstUpNic.Name}");
                return true;
            }
            else
            {
                Console.WriteLine($"Failed to remove IP with error code {result}, LastError: {Marshal.GetLastWin32Error()}");
                return false;
            }
        }

        private static int GetInterfaceIndex(NetworkInterface nic)
        {
            try
            {
                var ipProps = nic.GetIPProperties();
                return ipProps.GetIPv6Properties()?.Index ?? 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
