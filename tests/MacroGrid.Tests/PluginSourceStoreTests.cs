using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class PluginSourceStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static PluginSource Source(string id = "someone/repo") => new(id, "someone", "repo", "Someone's Plugins", DateTimeOffset.UtcNow);

    [Fact]
    public void Added_sources_persist_across_instances()
    {
        new PluginSourceStore(_root).Add(Source());

        var reloaded = new PluginSourceStore(_root).All();
        Assert.Equal("someone/repo", Assert.Single(reloaded).Id);
    }

    [Fact]
    public void Adding_the_same_source_twice_is_a_no_op()
    {
        var store = new PluginSourceStore(_root);
        Assert.True(store.Add(Source()));
        Assert.False(store.Add(Source()));
        Assert.Single(store.All());
    }

    [Fact]
    public void Removing_an_unknown_id_reports_false()
    {
        var store = new PluginSourceStore(_root);
        store.Add(Source());
        Assert.False(store.Remove("nope/nope"));
        Assert.True(store.Remove("someone/repo"));
        Assert.Empty(store.All());
    }
}

public sealed class PluginSourceUrlsRepoParsingTests
{
    [Theory]
    [InlineData("https://github.com/Deccoyi/macro-grid-plugin", "Deccoyi", "macro-grid-plugin")]
    [InlineData("https://github.com/Deccoyi/macro-grid-plugin/", "Deccoyi", "macro-grid-plugin")]
    [InlineData("http://github.com/Deccoyi/macro-grid-plugin.git", "Deccoyi", "macro-grid-plugin")]
    [InlineData("Deccoyi/macro-grid-plugin", "Deccoyi", "macro-grid-plugin")]
    [InlineData("  Deccoyi/macro-grid-plugin  ", "Deccoyi", "macro-grid-plugin")]
    public void Parses_a_repository_reference(string input, string owner, string repo)
    {
        Assert.True(PluginSourceUrls.TryParseRepo(input, out var actualOwner, out var actualRepo));
        Assert.Equal(owner, actualOwner);
        Assert.Equal(repo, actualRepo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/extra/path")]
    [InlineData("owner")]
    public void Refuses_anything_else(string input) =>
        Assert.False(PluginSourceUrls.TryParseRepo(input, out _, out _));
}
