using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>The opt-in to larger incoming web socket messages (<c>"limits": { "wsMessageKB": 256 }</c> in plugin.json). It is only for an
/// app on the same PC, so it counts only when a <c>ws:</c> permission names <c>localhost</c> or <c>127.0.0.1</c>, and the person approves
/// it like a permission (<see cref="ApprovalKey"/>).</summary>
public static class JsWsLimits
{
    public const int MinKB = 64;
    public const int MaxKB = 1024;

    /// <summary>The approval entry the person sees and can switch off; it is not a permission a script can ask for.</summary>
    public const string ApprovalKey = "limit:ws-large-messages";

    public static bool InRange(int kb) => kb is >= MinKB and <= MaxKB;

    /// <summary>Only these two names: a name like <c>x.localhost</c> or another address of 127.0.0.0/8 is not enough.</summary>
    public static bool IsLoopbackHost(string host) => host.Trim('[', ']').ToLowerInvariant() is "localhost" or "127.0.0.1" or "::1";

    public static bool HasLoopbackPermission(IEnumerable<string>? permissions) =>
        (permissions ?? []).Any(p => p.StartsWith("ws:", StringComparison.OrdinalIgnoreCase)
            && p.LastIndexOf(':') is > 3 and var colon && IsLoopbackHost(p[3..colon]));

    /// <summary>The approval entry this manifest needs, or null when it asks for nothing (or nothing valid).</summary>
    public static string? ApprovalKeyFor(PluginManifest manifest) =>
        manifest.Kind == PluginKind.Js && manifest.Limits?.WsMessageKB is { } kb && kb > MinKB && InRange(kb) && HasLoopbackPermission(manifest.Permissions)
            ? ApprovalKey : null;

    /// <summary>What the approval screens list for a manifest: its permissions plus the approval entry of a valid opt-in.</summary>
    public static string[] ListedPermissions(PluginManifest manifest) =>
        [.. manifest.Permissions ?? [], .. (ApprovalKeyFor(manifest) is { } key ? new[] { key } : [])];

    /// <summary>The incoming message limit for one connection, in bytes.</summary>
    public static int MessageBytes(PluginManifest manifest, bool approved, string host, int defaultBytes) =>
        approved && manifest.Limits?.WsMessageKB is { } kb && InRange(kb) && IsLoopbackHost(host) ? Math.Max(kb * 1024, defaultBytes) : defaultBytes;
}
