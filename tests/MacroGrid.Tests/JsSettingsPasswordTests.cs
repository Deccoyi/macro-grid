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
