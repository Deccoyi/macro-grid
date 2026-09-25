using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginStatusRegistryTests
{
    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public void A_core_item_with_a_lifetime_disappears_after_it()
    {
        var time = new ManualTime();
        var registry = new PluginStatusRegistry(time);
        registry.SetCore("actionError", "failed", StatusLevel.Warning, lifetime: TimeSpan.FromSeconds(15));

        time.Advance(TimeSpan.FromSeconds(14));
        Assert.Contains(registry.All, i => i.Id == "actionError");

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(registry.All, i => i.Id == "actionError");
    }

    [Fact]
    public void Setting_the_item_again_restarts_its_lifetime()
    {
        var time = new ManualTime();
        var registry = new PluginStatusRegistry(time);
        registry.SetCore("actionError", "first", StatusLevel.Warning, lifetime: TimeSpan.FromSeconds(15));
        time.Advance(TimeSpan.FromSeconds(10));
        registry.SetCore("actionError", "second", StatusLevel.Warning, lifetime: TimeSpan.FromSeconds(15));

        time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal("second", registry.All.Single(i => i.Id == "actionError").Text);
    }

    [Fact]
    public void An_item_without_a_lifetime_stays()
    {
        var time = new ManualTime();
        var registry = new PluginStatusRegistry(time);
        registry.SetCore("devices", "2 devices", StatusLevel.Ok);

        time.Advance(TimeSpan.FromDays(1));
        Assert.Contains(registry.All, i => i.Id == "devices");
    }

    [Fact]
    public void RemoveCore_drops_the_item_and_ignores_unknown_ids()
    {
        var registry = new PluginStatusRegistry(new ManualTime());
        registry.SetCore("actionError", "failed", StatusLevel.Warning, lifetime: TimeSpan.FromSeconds(15));

        registry.RemoveCore("actionError");
        registry.RemoveCore("nothing-here");

        Assert.Empty(registry.All);
    }
}
