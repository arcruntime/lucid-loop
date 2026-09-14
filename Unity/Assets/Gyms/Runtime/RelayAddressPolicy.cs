using System;
using System.Net;
using System.Net.Sockets;

namespace LucidLoop.Gyms
{
    public static class RelayAddressPolicy
    {
        public static bool DevelopmentLanAllowed
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || LUCID_LOOP_INTERNAL_TESTFLIGHT
                return true;
#else
                return false;
#endif
            }
        }

        public static Uri Validate(string address)
        {
            var uri = new Uri(address, UriKind.Absolute);
            bool permitted = uri.Scheme == "wss" || (uri.Scheme == "ws" &&
                (uri.IsLoopback || (DevelopmentLanAllowed && IsDevelopmentLanHost(uri.Host))));
            if (!permitted || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("Use wss remotely; ws permits loopback and development-build private LAN addresses only. URL credentials, queries and fragments are not supported.");
            return uri;
        }

        public static bool IsDevelopmentLanHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) return false;
            string name = host.Trim('[', ']').TrimEnd('.');
            if (name.EndsWith(".local", StringComparison.OrdinalIgnoreCase) && name.Length > 6) return true;
            if (!IPAddress.TryParse(name, out var address)) return false;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            var bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                    (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 169 && bytes[1] == 254);
            return address.AddressFamily == AddressFamily.InterNetworkV6 &&
                ((bytes[0] & 0xfe) == 0xfc || (bytes[0] == 0xfe && (bytes[1] & 0xc0) == 0x80));
        }
    }
}
