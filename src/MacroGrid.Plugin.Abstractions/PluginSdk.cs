using System.Reflection;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>
/// The version of the Plugin SDK, which is also the version of Macro Grid: the server and the SDK carry one number
/// (docs/guides/versioning.md). It is read from this assembly, whose version is set once, by <c>&lt;Version&gt;</c> in the
/// repository's Directory.Build.props, so there is no second place to keep in step. A plugin declares the oldest
/// Macro Grid it runs on in its manifest's <c>macroGrid</c> field.
/// </summary>
public static class PluginSdk
{
    /// <summary>MAJOR.MINOR.PATCH, without a pre-release label or build metadata (for example "1.0.0").</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var assembly = typeof(PluginSdk).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // "1.0.0+<commit>" carries build metadata after the '+', "1.0.0-beta" a label after the '-'.
            var end = informational.IndexOfAny(['+', '-']);
            return end < 0 ? informational : informational[..end];
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
    }
}
