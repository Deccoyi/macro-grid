using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class JsSettingsCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-jscache-" + Guid.NewGuid().ToString("N"));

    private static readonly SettingField[] Fields =
    [
        new("name", "Name", SettingFieldKind.Text),
        new("apiKey", "Key", SettingFieldKind.Password),
    ];

    private sealed class CountingSecrets : IPluginSecrets
    {
        public int Unprotects;
        public string Protect(string secret) => "p:" + secret;
        public string? Unprotect(string protectedSecret) { Unprotects++; return protectedSecret[2..]; }
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void Repeated_loads_decrypt_once_and_hand_out_copies()
    {
        var secrets = new CountingSecrets();
        var page = new JsSettingsPage(_dir, Fields, secrets);
        page.Save(new JsonObject { ["name"] = "a", ["apiKey"] = "key" });

        var first = page.Load();
        first["apiKey"] = "";
        first["name"] = "changed";
        var second = page.Load();
        page.Load();

        Assert.Equal("key", (string?)second["apiKey"]);
        Assert.Equal("a", (string?)second["name"]);
        Assert.Equal(1, secrets.Unprotects);
    }

    [Fact]
    public void Save_and_a_file_replaced_behind_the_page_are_seen()
    {
        var page = new JsSettingsPage(_dir, Fields, new CountingSecrets());
        page.Save(new JsonObject { ["name"] = "a" });
        Assert.Equal("a", (string?)page.Load()["name"]);

        page.Save(new JsonObject { ["name"] = "b" });
        Assert.Equal("b", (string?)page.Load()["name"]);

        // Like a backup restore: another writer replaces the file.
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{\"name\":\"restored-value\"}");
        Assert.Equal("restored-value", (string?)page.Load()["name"]);
    }

    [Fact]
    public void Pages_do_not_share_a_cache()
    {
        var other = _dir + "-other";
        try
        {
            var a = new JsSettingsPage(_dir, Fields, new CountingSecrets());
            var b = new JsSettingsPage(other, Fields, new CountingSecrets());
            a.Save(new JsonObject { ["apiKey"] = "secret-a" });
            a.Load();
            Assert.Null(b.Load()["apiKey"]);
        }
        finally { try { Directory.Delete(other, recursive: true); } catch (IOException) { } }
    }
}
