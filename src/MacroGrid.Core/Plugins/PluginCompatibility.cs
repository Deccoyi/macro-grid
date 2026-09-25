using System.Text.RegularExpressions;

namespace MacroGrid.Core.Plugins;

/// <summary>Whether a plugin runs on a Macro Grid version, and if not, why (shown in the editor).</summary>
public readonly record struct PluginCompatibilityResult(bool Compatible, string? Reason)
{
    public static readonly PluginCompatibilityResult Ok = new(true, null);
}

/// <summary>
/// The one place that decides whether a plugin fits this Macro Grid (docs/guides/versioning.md). A plugin's manifest, or an
/// entry in a source index, says <c>"macroGrid": "1.3.0"</c>: the oldest version it runs on, and it runs on every later
/// version of the same MAJOR.
/// <para>Older manifests carry <c>sdkVersion</c> and <c>minServerVersion</c> instead. Those built for SDK 0.4.x are treated as
/// <c>macroGrid: 1.0.0</c>, because 1.0.0 did not change what a 0.4.x plugin uses; anything older must be rebuilt.</para>
/// </summary>
public static partial class PluginCompatibility
{
    /// <summary>What a legacy 0.4.x manifest counts as.</summary>
    public const string LegacyBaseline = "1.0.0";

    [GeneratedRegex(@"^\^?0\.4\.\d+$")]
    private static partial Regex Sdk04Pattern();

    /// <summary>The oldest Macro Grid the manifest asks for, or null with the reason it cannot be understood or is too old.</summary>
    public static string? Required(string? macroGrid, string? sdkVersion, out string? reason)
    {
        reason = null;
        if (!string.IsNullOrWhiteSpace(macroGrid))
        {
            if (SemVer.IsThreePartVersion(macroGrid)) return macroGrid;
            reason = $"macroGrid must be MAJOR.MINOR.PATCH such as \"1.0.0\", not \"{macroGrid}\"";
            return null;
        }

        if (string.IsNullOrWhiteSpace(sdkVersion))
        {
            reason = "The manifest has no macroGrid field";
            return null;
        }

        if (Sdk04Pattern().IsMatch(sdkVersion.Trim())) return LegacyBaseline;

        reason = $"Built for an older SDK ({sdkVersion}); it must be rebuilt for Macro Grid {LegacyBaseline}";
        return null;
    }

    public static PluginCompatibilityResult Check(string macroGridVersion, string? macroGrid, string? sdkVersion)
    {
        var required = Required(macroGrid, sdkVersion, out var reason);
        if (required is null) return new(false, reason);
        if (SemVer.SatisfiesPlatform(macroGridVersion, required)) return PluginCompatibilityResult.Ok;

        var differentMajor = SemVer.TryParse(macroGridVersion, out var server) && SemVer.TryParse(required, out var wanted) && server.Major != wanted.Major;
        return new(false, differentMajor
            ? $"Built for Macro Grid {required}; this is {macroGridVersion}, so the plugin must be rebuilt"
            : $"Needs Macro Grid {required} or newer, this is {macroGridVersion}");
    }
}
