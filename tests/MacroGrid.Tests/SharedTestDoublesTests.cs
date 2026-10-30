using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Plugin.Testing;
using MacroGrid.Tests.StubPlugin;

namespace MacroGrid.Tests;

public sealed class SharedTestDoublesTests
{
    [Fact]
    public void A_plugin_initialized_on_the_fake_host_leaves_its_registrations_in_lists()
    {
        var host = new FakePluginHost(Path.GetTempPath());

        new StubPlugin.StubPlugin().Initialize(host);

        Assert.Single(host.Actions);
        Assert.Single(host.VariableProviders);
        Assert.NotNull(host.SettingsPage);
        Assert.Equal("stub ready", host.StatusItems["stub"].LastText);
        Assert.Equal(StatusLevel.Ok, host.StatusItems["stub"].LastLevel);
    }

    [Fact]
    public void The_variable_store_records_sets_and_removes()
    {
        var store = new FakeVariableStore();
        store.Set("a.x", 1d);
        store.Set("a.x", 2d);
        store.Remove("a.y");

        Assert.Equal(2d, store.Get("a.x"));
        Assert.True(store.WasEverSet("a.x", 1d));
        Assert.False(store.WasEverSet("a.x", 3d));
        Assert.Equal(["a.y"], store.RemovedNames);
    }

    [Fact]
    public void Secrets_round_trip_and_are_not_the_plain_text()
    {
        var secrets = new FakePluginSecrets();
        var protectedText = secrets.Protect("pw");

        Assert.NotEqual("pw", protectedText);
        Assert.Equal("pw", secrets.Unprotect(protectedText));
        Assert.Null(secrets.Unprotect("pw"));
    }

    [Fact]
    public void Widget_posts_are_recorded()
    {
        var host = new FakePluginHost(Path.GetTempPath());

        host.Widgets.Post("gauge", "value", JsonValue.Create(5), retain: true);

        var post = Assert.Single(host.WidgetPosts.Posts);
        Assert.Equal(("gauge", "value", true), (post.Widget, post.Name, post.Retain));
    }
}
