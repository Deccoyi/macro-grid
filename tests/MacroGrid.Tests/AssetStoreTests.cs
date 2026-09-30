using System.Text.Json.Nodes;
using MacroGrid.Core.Widgets;

namespace MacroGrid.Tests;

public class AssetStoreTests
{
    private static string BigDataUri(char fill = 'A') => "data:image/svg+xml;base64," + new string(fill, 300);

    [Fact]
    public void Externalize_replaces_large_data_uris_anywhere_in_the_tree_with_references()
    {
        var store = new AssetStore();
        var uri = BigDataUri();
        var tree = new JsonObject
        {
            ["pages"] = new JsonArray(new JsonObject
            {
                ["widgets"] = new JsonArray(new JsonObject
                {
                    ["style"] = new JsonObject { ["icon"] = uri },
                    ["props"] = new JsonObject { ["src"] = uri },
                }),
            }),
        };

        store.Externalize(tree);

        var widget = tree["pages"]![0]!["widgets"]![0]!;
        var icon = widget["style"]!["icon"]!.GetValue<string>();
        Assert.StartsWith(AssetStore.RefPrefix, icon);
        Assert.Equal(icon, widget["props"]!["src"]!.GetValue<string>());
        Assert.Equal(uri, store.Get(icon[AssetStore.RefPrefix.Length..]));
    }

    [Fact]
    public void Externalize_leaves_short_and_non_data_strings_alone()
    {
        var store = new AssetStore();
        var tree = JsonNode.Parse("""{"a":"data:image/png;base64,AAAA","b":"https://example.com/x.png","c":5,"d":null}""")!;

        store.Externalize(tree);

        Assert.Equal("data:image/png;base64,AAAA", tree["a"]!.GetValue<string>());
        Assert.Equal("https://example.com/x.png", tree["b"]!.GetValue<string>());
    }

    [Fact]
    public void The_same_content_always_gets_the_same_reference()
    {
        var store = new AssetStore();

        Assert.Equal(store.Put(BigDataUri('A')), store.Put(BigDataUri('A')));
        Assert.NotEqual(store.Put(BigDataUri('A')), store.Put(BigDataUri('B')));
    }

    [Fact]
    public void An_unknown_hash_returns_null()
    {
        Assert.Null(new AssetStore().Get("0123456789abcdef01234567"));
    }

    [Fact]
    public void The_oldest_assets_are_dropped_when_the_size_budget_is_exceeded()
    {
        var store = new AssetStore(maxEntries: 100, maxChars: 700);
        var a = store.Put(BigDataUri('A'));
        var b = store.Put(BigDataUri('B'));
        var c = store.Put(BigDataUri('C'));

        Assert.Null(store.Get(a[AssetStore.RefPrefix.Length..]));
        Assert.NotNull(store.Get(b[AssetStore.RefPrefix.Length..]));
        Assert.NotNull(store.Get(c[AssetStore.RefPrefix.Length..]));
    }

    [Fact]
    public void A_single_asset_over_the_budget_is_still_kept()
    {
        var store = new AssetStore(maxEntries: 100, maxChars: 10);
        var a = store.Put(BigDataUri('A'));

        Assert.NotNull(store.Get(a[AssetStore.RefPrefix.Length..]));
    }
}
