namespace MacroGrid.Core.Devices;

/// <summary>Decides whether a browser may open the <c>/ws</c> socket. Phones and the browser deck connect with a device token or the
/// pairing PIN, and a page cannot read the token, so this is defence in depth against a page (for example one inside a web widget)
/// guessing PINs. Only a browser sends <c>Origin</c>: no header (a native client) passes; a header must be the server's own origin
/// (the browser deck, on any address the person opened it by) or the phone app's (`localhost`, plain or TLS).</summary>
public static class WebSocketOriginGuard
{
    private static readonly string[] AppOrigins = ["https://localhost", "http://localhost", "capacitor://localhost"];

    /// <param name="origin">The <c>Origin</c> header, or null when none was sent.</param>
    /// <param name="host">The <c>Host</c> header of the same request.</param>
    public static bool IsAllowed(string? origin, string? host)
    {
        if (origin is null)
            return true;
        if (AppOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            return true;
        // The deck is served by this server, so its origin is the scheme plus the very Host the request was sent to.
        return !string.IsNullOrEmpty(host)
            && (origin.Equals("http://" + host, StringComparison.OrdinalIgnoreCase) || origin.Equals("https://" + host, StringComparison.OrdinalIgnoreCase));
    }
}
