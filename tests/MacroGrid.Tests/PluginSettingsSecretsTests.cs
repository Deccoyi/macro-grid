using System.Text.Json.Nodes;
using MacroGrid.Core.Backup;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginSettingsSecretsTests
{
    private static readonly SettingField[] Fields =
    [
        new("pin", "Pin", SettingFieldKind.Password),
        new("rows", "Rows", SettingFieldKind.List)
        {
            ItemFields = [new SettingField("name", "Name", SettingFieldKind.Text), new SettingField("token", "Token", SettingFieldKind.Password)],
        },
    ];

    private static JsonObject Settings(params (string Name, string Token)[] rows) => new()
    {
        ["pin"] = "p",
        ["rows"] = new JsonArray(rows.Select(r => (JsonNode)new JsonObject { ["name"] = r.Name, ["token"] = r.Token }).ToArray()),
    };

    private static string[] Tokens(JsonObject v) => v["rows"]!.AsArray().Select(r => r!["token"]?.GetValue<string>() ?? "<missing>").ToArray();

    [Fact]
    public void Blank_clears_top_level_and_row_passwords()
    {
        var shown = PluginSettingsSecrets.Blank(Fields, Settings(("a", "s1"), ("b", "s2")));

        Assert.Equal("", shown["pin"]!.GetValue<string>());
        Assert.Equal(["", ""], Tokens(shown));
        Assert.DoesNotContain("s1", shown.ToJsonString());
    }

    [Fact]
    public void An_unchanged_form_gets_every_password_back()
    {
        var stored = Settings(("a", "s1"), ("b", "s2"));
        var form = PluginSettingsSecrets.Blank(Fields, (JsonObject)stored.DeepClone());

        PluginSettingsSecrets.RestoreBlankPasswords(Fields, form, stored);

        Assert.Equal("p", form["pin"]!.GetValue<string>());
        Assert.Equal(["s1", "s2"], Tokens(form));
    }

    [Fact]
    public void A_typed_row_password_is_kept_and_a_reordered_row_never_gets_another_rows_secret()
    {
        var stored = Settings(("a", "s1"), ("b", "s2"));
        var form = Settings(("b", ""), ("a", ""));

        PluginSettingsSecrets.RestoreBlankPasswords(Fields, form, stored);
        Assert.Equal(["", ""], Tokens(form));

        var typed = Settings(("a", "new"), ("b", ""));
        PluginSettingsSecrets.RestoreBlankPasswords(Fields, typed, stored);
        Assert.Equal(["new", "s2"], Tokens(typed));
    }

    [Fact]
    public void A_deleted_row_does_not_shift_secrets()
    {
        var stored = Settings(("a", "s1"), ("b", "s2"), ("c", "s3"));

        var firstDeleted = Settings(("b", ""), ("c", ""));
        PluginSettingsSecrets.RestoreBlankPasswords(Fields, firstDeleted, stored);
        Assert.Equal(["", ""], Tokens(firstDeleted));

        var lastDeleted = Settings(("a", ""), ("b", ""));
        PluginSettingsSecrets.RestoreBlankPasswords(Fields, lastDeleted, stored);
        Assert.Equal(["s1", "s2"], Tokens(lastDeleted));
    }

    [Fact]
    public void A_backup_without_passwords_keeps_the_stored_ones_for_the_same_rows_only()
    {
        var stored = Settings(("a", "s1"), ("b", "s2"));
        var fromBackup = PluginSettingsSecrets.Strip(Fields, Settings(("a", "x"), ("z", "y")));
        Assert.Equal(["<missing>", "<missing>"], Tokens(fromBackup));
        Assert.False(fromBackup.ContainsKey("pin"));

        PluginSettingsSecrets.RestoreBlankPasswords(Fields, fromBackup, stored);

        Assert.Equal("p", fromBackup["pin"]!.GetValue<string>());
        Assert.Equal(["s1", "<missing>"], Tokens(fromBackup));
    }
}
