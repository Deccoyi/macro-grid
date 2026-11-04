using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Plugin.Testing;

namespace MacroGrid.Tests;

public sealed class JsSettingsPasswordTests : IDisposable
{
    private const string Secret = "sk-very-secret-value";
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-jspw-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _unreadable = [];

    private static readonly SettingField[] Fields =
    [
        new("name", "Name", SettingFieldKind.Text),
        new("apiKey", "Key", SettingFieldKind.Password),
        new("rows", "Rows", SettingFieldKind.List)
        {
            ItemFields = [new SettingField("token", "Token", SettingFieldKind.Password)],
        },
    ];

    /// <summary>Base64, so the plain value is really absent from the stored text.</summary>
    private sealed class OpaqueSecrets : IPluginSecrets
    {
        public string Protect(string secret) => "opaque:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(secret));

        public string? Unprotect(string protectedSecret) =>
            protectedSecret.StartsWith("opaque:", StringComparison.Ordinal)
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedSecret["opaque:".Length..])) : null;
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private JsSettingsPage Page(IPluginSecrets? secrets = null) => new(_dir, Fields, secrets ?? new OpaqueSecrets(), _unreadable.Add);

    private string Raw() => File.ReadAllText(Path.Combine(_dir, "settings.json"));

    [Fact]
    public void A_password_is_stored_protected_and_loads_plain()
    {
        var page = Page();
        page.Save(new JsonObject
        {
            ["name"] = "n", ["apiKey"] = Secret,
            ["rows"] = new JsonArray(new JsonObject { ["token"] = Secret }),
        });

        var raw = Raw();
        Assert.DoesNotContain(Secret, raw);
        Assert.Contains(JsSettingsPage.ProtectedPrefix, raw);
        var loaded = page.Load();
        Assert.Equal(Secret, loaded["apiKey"]!.GetValue<string>());
        Assert.Equal(Secret, loaded["rows"]![0]!["token"]!.GetValue<string>());
        Assert.Equal("n", loaded["name"]!.GetValue<string>());
        Assert.Empty(_unreadable);
    }

    [Fact]
    public void An_old_plain_value_still_loads_and_is_protected_by_the_next_save()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), $$"""{ "apiKey": "{{Secret}}" }""");
        var page = Page();

        Assert.Equal(Secret, page.Load()["apiKey"]!.GetValue<string>());

        page.Save(page.Load());
        Assert.DoesNotContain(Secret, Raw());
        Assert.Equal(Secret, page.Load()["apiKey"]!.GetValue<string>());
    }

    [Fact]
    public void An_old_plain_value_is_protected_on_disk_at_the_first_load_including_list_rows()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), $$"""{ "name": "n", "undeclared": 1, "apiKey": "{{Secret}}", "rows": [ { "token": "{{Secret}}" } ] }""");
        var page = Page();

        var loaded = page.Load();

        Assert.DoesNotContain(Secret, Raw());
        Assert.Contains("undeclared", Raw());
        Assert.Equal(Secret, loaded["apiKey"]!.GetValue<string>());
        Assert.Equal(Secret, loaded["rows"]![0]!["token"]!.GetValue<string>());
        Assert.Equal(Secret, page.Load()["apiKey"]!.GetValue<string>());
    }

    [Fact]
    public void A_password_that_is_not_text_is_refused()
    {
        var page = Page();

        Assert.Throws<InvalidOperationException>(() => page.Save(new JsonObject { ["apiKey"] = 1234 }));
        Assert.Throws<InvalidOperationException>(() => page.Save(new JsonObject { ["rows"] = new JsonArray(new JsonObject { ["token"] = true }) }));
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void A_protector_failure_is_a_clean_refusal()
    {
        var page = new JsSettingsPage(_dir, Fields, new ThrowingSecrets());

        Assert.Throws<InvalidOperationException>(() => page.Save(new JsonObject { ["apiKey"] = Secret }));
    }

    private sealed class ThrowingSecrets : IPluginSecrets
    {
        public string Protect(string secret) => throw new System.Security.Cryptography.CryptographicException("no");
        public string? Unprotect(string protectedSecret) => null;
    }

    [Fact]
    public void A_value_that_cannot_be_unprotected_loads_empty_and_is_reported_without_the_value()
    {
        Page().Save(new JsonObject { ["apiKey"] = Secret });
        var raw = Raw().Replace("opaque:", "other-pc:");
        File.WriteAllText(Path.Combine(_dir, "settings.json"), raw);

        var loaded = Page().Load();

        Assert.Equal("", loaded["apiKey"]!.GetValue<string>());
        Assert.Equal(["apiKey"], _unreadable);
    }

    [Fact]
    public void The_unreadable_notice_is_a_problem_line_that_a_save_clears_and_never_holds_the_value()
    {
        var problems = new ProblemList();
        var page = new JsSettingsPage(_dir, Fields, new OpaqueSecrets(),
            key => problems.Report("p", "P", ProblemSeverity.Warning, ProblemCodes.SecretUnreadable, $"cannot read '{key}'", key, 20),
            () => problems.Resolve("p", ProblemCodes.SecretUnreadable));
        page.Save(new JsonObject { ["apiKey"] = Secret });
        File.WriteAllText(Path.Combine(_dir, "settings.json"), Raw().Replace("opaque:", "other-pc:"));

        page.Load();
        var line = Assert.Single(problems.Snapshot());
        Assert.Equal(ProblemCodes.SecretUnreadable, line.Code);
        Assert.DoesNotContain(Secret, line.Message);

        page.Save(new JsonObject { ["apiKey"] = "new" });
        Assert.Empty(problems.Snapshot());
    }

    private static IPluginSecrets RealSecrets(string pluginId) =>
        new MacroGrid.Core.Plugins.PluginHostCollector("1.0.0", Path.GetTempPath(), pluginId, new MacroGrid.Core.Plugins.PluginStatusRegistry(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, new MacroGrid.Windows.Security.DpapiSecretProtector()).Secrets;

    private JsSettingsPage RealPage(string pluginId) => new(_dir, Fields, RealSecrets(pluginId), _unreadable.Add);

    [Fact]
    public void A_real_dpapi_password_round_trips_with_the_v2_prefix()
    {
        RealPage("plugin.a").Save(new JsonObject { ["apiKey"] = Secret });

        Assert.Contains("\"mgp2:", Raw());
        Assert.DoesNotContain("mgp1:", Raw());
        Assert.Equal(Secret, RealPage("plugin.a").Load()["apiKey"]!.GetValue<string>());
        Assert.Empty(_unreadable);
    }

    [Fact]
    public void A_v2_password_of_one_plugin_is_unreadable_under_another_plugin()
    {
        RealPage("plugin.a").Save(new JsonObject { ["apiKey"] = Secret });

        var loaded = RealPage("plugin.b").Load();

        Assert.Equal("", loaded["apiKey"]!.GetValue<string>());
        Assert.Equal(["apiKey"], _unreadable);
    }

    [Fact]
    public void A_v1_password_still_loads_and_the_next_save_writes_v2()
    {
        // mgp1: = DPAPI without per-plugin entropy, as written by earlier dev builds.
        var legacy = new MacroGrid.Windows.Security.DpapiSecretProtector().Protect(Secret);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), $$"""{ "apiKey": "mgp1:{{legacy}}" }""");
        var page = RealPage("plugin.a");

        var loaded = page.Load();
        Assert.Equal(Secret, loaded["apiKey"]!.GetValue<string>());
        Assert.Contains("mgp1:", Raw());

        page.Save(loaded);
        Assert.DoesNotContain("mgp1:", Raw());
        Assert.Contains("\"mgp2:", Raw());
        Assert.Equal(Secret, page.Load()["apiKey"]!.GetValue<string>());
        Assert.Empty(_unreadable);
    }

    [Theory]
    [InlineData("mgp2:not-base64!!")]
    [InlineData("mgp2:AAAA")]
    [InlineData("mgp1:AAAA")]
    [InlineData("mgp2:")]
    public void Garbage_after_a_protected_prefix_is_unreadable_not_an_exception(string stored)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "settings.json"), $$"""{ "apiKey": "{{stored}}" }""");

        var loaded = RealPage("plugin.a").Load();

        Assert.Equal("", loaded["apiKey"]!.GetValue<string>());
        Assert.Equal(["apiKey"], _unreadable);
    }

    [Fact]
    public void A_host_without_a_protector_refuses_to_save_a_password()
    {
        var page = new JsSettingsPage(_dir, Fields, secrets: null);

        Assert.Throws<InvalidOperationException>(() => page.Save(new JsonObject { ["apiKey"] = Secret }));
        Assert.False(File.Exists(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void A_backup_collector_strip_removes_the_password()
    {
        var page = Page();
        page.Save(new JsonObject { ["name"] = "n", ["apiKey"] = Secret });

        var stripped = MacroGrid.Core.Backup.PluginSettingsSecrets.Strip(page.Fields, page.Load());

        Assert.False(stripped.ContainsKey("apiKey"));
        Assert.DoesNotContain(Secret, stripped.ToJsonString());
    }
}
