using System.Net;

namespace MacroGrid.Core.Devices;

/// <summary>Decides whether a request came from the machine the server runs on.</summary>
public static class LoopbackGuard
{
    /// <summary>True for 127.0.0.1, ::1 and the IPv4-mapped form (::ffff:127.0.0.1). An unknown address
    /// (null) is not trusted.</summary>
    public static bool IsLoopback(IPAddress? address) => address is not null && IPAddress.IsLoopback(address);
}
