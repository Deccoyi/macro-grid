namespace MacroGrid.Core.Devices;

/// <summary>Refuses an editor API request whose <c>Host</c> header is not a loopback name with one of the server's own ports. A page that
/// uses DNS rebinding (its own name first points at its server, then at 127.0.0.1) reaches <c>/api</c> from a loopback address and, being
/// same-origin in the browser, sends no <c>Origin</c> header on a <c>GET</c>, so <see cref="OriginGuard"/> lets it through; its
/// <c>Host</c> header still carries the name it used, and that is what this checks.</summary>
public static class HostGuard
{
    private static readonly string[] Names = ["localhost", "127.0.0.1", "[::1]"];

    /// <summary>The plain and TLS ports of the server, and the Vite dev servers (editor 5190, browser deck 5192) that proxy `/api` to it.</summary>
    private static readonly string[] Ports =
#if DEBUG
        int.TryParse(Environment.GetEnvironmentVariable("MACROGRID_PORT"), out var devPort) ? ["9820", "9821", "5190", "5192", devPort.ToString(), (devPort + 1).ToString()] :
#endif
        ["9820", "9821", "5190", "5192"];

    public static bool IsAllowed(string? host)
    {
        if (string.IsNullOrEmpty(host))
            return false;
        var colon = host.LastIndexOf(':');
        // "[::1]" has colons inside the brackets; only a colon after the closing bracket starts a port.
        if (colon < 0 || colon < host.LastIndexOf(']'))
            return false;
        return Names.Contains(host[..colon], StringComparer.OrdinalIgnoreCase) && Ports.Contains(host[(colon + 1)..]);
    }
}
