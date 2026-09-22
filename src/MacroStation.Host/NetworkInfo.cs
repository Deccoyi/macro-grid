using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MacroStation.Host;

internal static class NetworkInfo
{
    /// <summary>IPv4 addresses of active LAN adapters, adapters with a default gateway first.</summary>
    public static IReadOnlyList<IPAddress> GetLanAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .Select(n => n.GetIPProperties())
            .OrderByDescending(p => p.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
            .SelectMany(p => p.UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a)
                        && !a.ToString().StartsWith("169.254.", StringComparison.Ordinal))
            .Distinct()
            .ToList();
}
