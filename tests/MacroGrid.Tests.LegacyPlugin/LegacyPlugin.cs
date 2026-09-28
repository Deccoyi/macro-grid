using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests.LegacyPlugin;

/// <summary>
/// A minimal plugin compiled against the real, already-published SDK 0.4.0 package (the last version before the
/// server and the SDK were unified into one version, docs/guides/versioning.md). Loaded by
/// <c>LegacyPluginCompatibilityTests</c> with a legacy manifest (<c>sdkVersion: "^0.4.0"</c>, no <c>macroGrid</c>)
/// to prove that a plugin built before the unification still loads on the current server, not just that the
/// compatibility rule says it should.
/// </summary>
public sealed class LegacyPlugin : IPlugin
{
    public void Initialize(IPluginHost host)
    {
    }
}
