using System.Text.RegularExpressions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>
/// The permission strings a JS plugin declares in <c>plugin.json</c> and the user approves:
/// <c>variables</c> (read variables, publish its own), <c>actions</c> (register actions),
/// <c>input</c> (send key presses / type text on the PC), <c>storage</c> (keep a small amount of its own data), <c>notify</c> (show a short notice), <c>ws:&lt;host&gt;:&lt;port&gt;</c> (open a web socket to exactly that host and port) and <c>http:&lt;host&gt;:&lt;port&gt;</c> (make HTTP
/// requests to exactly that host and port). Timers, settings pages, status items and logging need no permission.
/// </summary>
public sealed partial class JsPermissions(IEnumerable<string> granted)
{
    public const string Variables = "variables";
    public const string Actions = "actions";
    public const string Input = "input";
    public const string Storage = "storage";
    public const string Notify = "notify";
    private const string HttpPrefix = "http:";
    private const string WsPrefix = "ws:";

    private readonly HashSet<string> _granted = new(granted, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Granted => _granted;

    public bool Has(string permission) => _granted.Contains(permission);

    /// <summary>True if an HTTP request to this URL is covered by a granted <c>http:host:port</c> permission.</summary>
    public bool AllowsHttp(Uri url) =>
        (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
        && _granted.Contains($"{HttpPrefix}{url.Host}:{url.Port}");

    /// <summary>True if a web socket to this address (<c>ws</c> or <c>wss</c>) is covered by a granted <c>ws:host:port</c> permission. An <c>http:</c> permission does not allow a socket.</summary>
    public bool AllowsWs(Uri url) =>
        (url.Scheme == Uri.UriSchemeWs || url.Scheme == Uri.UriSchemeWss)
        && _granted.Contains($"{WsPrefix}{url.Host}:{url.Port}");

    /// <summary>Whether a permission string is one the runtime knows; a manifest with any other is refused.</summary>
    public static bool IsKnown(string permission) =>
        permission.Equals(Variables, StringComparison.OrdinalIgnoreCase)
        || permission.Equals(Actions, StringComparison.OrdinalIgnoreCase)
        || permission.Equals(Input, StringComparison.OrdinalIgnoreCase)
        || permission.Equals(Storage, StringComparison.OrdinalIgnoreCase)
        || permission.Equals(Notify, StringComparison.OrdinalIgnoreCase)
        || HttpPermission().IsMatch(permission)
        || WsPermission().IsMatch(permission);

    [GeneratedRegex(@"^ws:[A-Za-z0-9.\-]+:[0-9]{1,5}$", RegexOptions.IgnoreCase)]
    private static partial Regex WsPermission();

    [GeneratedRegex(@"^http:[A-Za-z0-9.\-]+:[0-9]{1,5}$", RegexOptions.IgnoreCase)]
    private static partial Regex HttpPermission();
}
