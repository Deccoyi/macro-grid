using System.Linq;

namespace MacroGrid.Core.Devices;

/// <summary>Decides whether a browser request to the editor API came from the editor itself, not some other
/// page open in the person's regular browser. <see cref="LoopbackGuard"/> alone cannot tell the two apart:
/// both connect from this same PC. Only a browser sends the <c>Origin</c> header, and only for a cross-origin
/// request or a same-origin one with a body (fetch/XHR); a plain same-origin <c>GET</c>, curl, or any other
/// non-browser client sends none, so a missing header is allowed through unchanged.</summary>
public static class OriginGuard
{
    /// <summary>The editor's own origins: the packaged app (port 9820, `localhost` and the loopback IPs
    /// WebView2 can use) and the Vite dev servers that proxy `/api` to it during development (editor 5190,
    /// browser deck 5192) — see `vite.config.ts` in each.</summary>
    private static readonly string[] Allowed =
    [
        "http://localhost:9820", "http://127.0.0.1:9820", "http://[::1]:9820",
        "http://localhost:5190", "http://localhost:5192",
    ];

    public static bool IsAllowed(string? origin) => origin is null || Allowed.Contains(origin);
}
