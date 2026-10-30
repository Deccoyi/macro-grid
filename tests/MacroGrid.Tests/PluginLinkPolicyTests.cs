using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginLinkPolicyTests : IDisposable
{
    private const string Official = "https://github.com/macro-grid/macro-grid-plugins";
    private const string Pasted = "https://github.com/someone/their-plugin";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-link-" + Guid.NewGuid().ToString("N"));

    public PluginLinkPolicyTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void An_id_that_is_not_installed_can_be_installed_from_a_link()
    {
        var origins = new PluginInstallOriginStore(_dir);
        Assert.False(PluginLinkPolicy.IsOtherSource(origins.Get("new-plugin"), Pasted));
    }

    [Fact]
    public void An_installed_official_id_is_not_replaced_from_a_pasted_link()
    {
        var origins = new PluginInstallOriginStore(_dir);
        origins.Set("obs", new PluginInstallOrigin(Official, "1.0.0", PluginTrust.Official));
        Assert.True(PluginLinkPolicy.IsOtherSource(origins.Get("obs"), Pasted));
    }

    [Fact]
    public void A_third_party_plugin_can_be_reinstalled_from_the_same_link_but_not_from_another()
    {
        var origins = new PluginInstallOriginStore(_dir);
        origins.Set("mine", new PluginInstallOrigin(Pasted, "1.0.0", PluginTrust.ThirdParty));
        Assert.False(PluginLinkPolicy.IsOtherSource(origins.Get("mine"), Pasted.ToUpperInvariant()));
        Assert.True(PluginLinkPolicy.IsOtherSource(origins.Get("mine"), "https://github.com/else/where"));
    }
}
