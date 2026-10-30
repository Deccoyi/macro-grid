namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>The "one source per plugin" rule: a plugin that was installed from one source is not replaced from another
/// (see "One source per plugin" in the plugin distribution design). A new package would inherit the grants and settings of the
/// installed one, so a pasted link must not be able to take over an official or otherwise sourced plugin.</summary>
public static class PluginLinkPolicy
{
    /// <summary>True when the id is already installed from a source other than <paramref name="sourceUrl"/>.
    /// An id with no recorded origin (a local folder install) is not another source.</summary>
    public static bool IsOtherSource(PluginInstallOrigin? existing, string sourceUrl) =>
        existing is not null && !string.Equals(existing.SourceUrl, sourceUrl, StringComparison.OrdinalIgnoreCase);
}
