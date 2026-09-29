using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Web;

/// <summary>Which addresses a <c>web</c> widget (or the <c>core.web</c> action) may show. The page is untrusted content from the internet, so
/// only <c>http</c> and <c>https</c> pages with no credentials in the address are allowed, and never this server itself or the phone app's own
/// origin (<c>localhost</c> and every other loopback form): such a page would be same-origin with the app. The renderers apply the same
/// rule (<c>isSafeWebUrl</c>); this copy makes the server refuse a bad address when a profile is saved or imported, not only when it is drawn.
/// The check runs on the parsed, normalized address, never on the raw text (the parser turns <c>0x7f.1</c> into <c>127.0.0.1</c>).</summary>
public static class WebUrlRule
{
    public const int MaxLength = 2048;

    public static bool IsAllowed(string? url) => TryGetHost(url, out _);

    /// <summary>True when <paramref name="url"/> may be shown; <paramref name="host"/> is then its host name (safe to log, unlike the full address).</summary>
    public static bool TryGetHost(string? url, out string host)
    {
        host = "";
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxLength || url != url.Trim())
            return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (uri.UserInfo.Length > 0 || url.Contains('\\'))
            return false;

        var name = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
        if (name.Length == 0 || uri.DnsSafeHost.EndsWith('.') || IsLoopbackName(name) || IsOwnAddress(name))
            return false;

        host = name;
        return true;
    }

    /// <summary>A safe-to-log label for an address: the host name of a usable one, or a fixed word for one that is not.</summary>
    public static string HostForLog(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host) ? uri.Host : "(not an address)";

    private static bool IsLoopbackName(string name)
    {
        if (name == "localhost" || name.EndsWith(".localhost", StringComparison.Ordinal))
            return true;
        if (!IPAddress.TryParse(name.Trim('[', ']'), out var ip))
            return false;
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        return IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any);
    }

    /// <summary>This PC's machine name and every address of its network adapters: a page from there is the server (or another local service).</summary>
    private static bool IsOwnAddress(string name)
    {
        if (name.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!IPAddress.TryParse(name.Trim('[', ']'), out var ip))
            return false;
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Any(a => a.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6 && a.Address.Equals(ip));
        }
        catch (NetworkInformationException)
        {
            return false;
        }
    }

    /// <summary>Clears every <c>web</c> widget address that fails the rule (the widget stays, as an empty cell) and returns the host names that were dropped.
    /// Called when a profile is saved or imported, so a bad address never reaches a phone.</summary>
    public static IReadOnlyList<string> Sanitize(Profile profile, ILogger? log = null)
    {
        var dropped = new List<string>();
        foreach (var widget in profile.Pages.SelectMany(p => p.Widgets))
        {
            if (widget.Type != WidgetTypes.Web || widget.Props is not { } props || !props.TryGetPropertyValue("url", out var node))
                continue;
            var value = node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
            if (string.IsNullOrEmpty(value) || IsAllowed(value))
                continue;
            props.Remove("url");
            var label = HostForLog(value);
            dropped.Add(label);
            log?.LogWarning(Diagnostics.SecurityEvents.WebUrlRefused, "Security: the address of a web widget was refused and cleared (host: {Host})", Diagnostics.SecurityEvents.ForLog(label));
        }
        return dropped;
    }
}
