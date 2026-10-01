using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Backup;
using MacroGrid.Core.Model;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;

namespace MacroGrid.Tests;

public class BackupFileTests
{
    private static Profile Sample(string id = "p1", string name = "Streaming") => new()
    {
        Id = id,
        Name = name,
        Pages = [new Page { Id = "a", Widgets = [new Widget { Id = "w1", Text = "Scene" }] }],
    };

    private static BackupContent Content() => new()
    {
        Profiles = [Sample(), Sample("p2", "Gaming")],
        ProfileTree = [new ProfileTreeNode { Type = "profile", Id = "p1" }, new ProfileTreeNode { Type = "profile", Id = "p2" }],
        Preferences = new AppPreferences { Theme = "light" },
        Variables = [new UserVariable("score", VariableType.Number, 3.0, true, "points")],
        Devices = [new BackupDevice("dev1", "Phone", "p2", true, false)],
        PluginSettings = { ["obs"] = new JsonObject { ["host"] = "localhost" } },
        LanguagePacks = { ["de"] = "{\"meta\":{\"tag\":\"de\"}}" },
    };

    private static byte[] Write(BackupContent? content = null) =>
        BackupFile.Write(content ?? Content(), BackupFile.KindBackup, "manual", "1.0.0", []);

    private static byte[] Rebuild(byte[] original, Action<ZipArchive> change)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using var source = new ZipArchive(new MemoryStream(original), ZipArchiveMode.Read);
            foreach (var entry in source.Entries)
            {
                using var from = entry.Open();
                using var to = zip.CreateEntry(entry.FullName).Open();
                from.CopyTo(to);
            }
            change(zip);
        }
        return buffer.ToArray();
    }

    private static void Add(ZipArchive zip, string name, string text)
    {
        using var stream = zip.CreateEntry(name).Open();
        stream.Write(Encoding.UTF8.GetBytes(text));
    }

    [Fact]
    public void Write_then_read_gives_the_same_content()
    {
        var read = BackupFile.Read(Write());

        Assert.Equal(BackupFile.KindBackup, read.Manifest.Kind);
        Assert.Equal("manual", read.Manifest.Reason);
        Assert.Equal(["p1", "p2"], read.Content.Profiles.Select(p => p.Id).Order());
        Assert.Equal(2, read.Content.ProfileTree.Count);
        Assert.Equal("light", read.Content.Preferences!.Theme);
        Assert.Equal("score", Assert.Single(read.Content.Variables).Name);
        Assert.Equal(new BackupDevice("dev1", "Phone", "p2", true, false), Assert.Single(read.Content.Devices));
        Assert.Equal("localhost", read.Content.PluginSettings["obs"]["host"]!.GetValue<string>());
        Assert.Contains("de", read.Content.LanguagePacks.Keys);
    }

    [Fact]
    public void The_hash_is_the_same_for_the_same_data_and_changes_with_it()
    {
        var first = BackupFile.Read(Write()).Manifest.ContentHash;
        var again = BackupFile.Read(Write()).Manifest.ContentHash;
        var changed = Content();
        changed.Profiles[0].Name = "Other";

        Assert.Equal(first, again);
        Assert.NotEqual(first, BackupFile.HashOf(changed));
        Assert.Equal(first, BackupFile.HashOf(Content()));
    }

    [Fact]
    public void No_secret_string_occurs_in_any_entry()
    {
        // A device has no token field and plugin settings are stripped before they reach the writer.
        var fields = new[]
        {
            new SettingField("host", "Host", SettingFieldKind.Text),
            new SettingField("password", "Password", SettingFieldKind.Password),
            new SettingField("accounts", "Accounts", SettingFieldKind.List)
            {
                ItemFields = [new SettingField("user", "User", SettingFieldKind.Text), new SettingField("secret", "Secret", SettingFieldKind.Password)],
            },
        };
        var values = new JsonObject
        {
            ["host"] = "localhost",
            ["password"] = "TOPLEVEL-SECRET",
            ["accounts"] = new JsonArray(new JsonObject { ["user"] = "ann", ["secret"] = "ROW-SECRET" }),
        };
        var content = Content();
        content.PluginSettings["obs"] = PluginSettingsSecrets.Strip(fields, values);

        var bytes = Write(content);
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            using var reader = new StreamReader(entry.Open());
            var text = reader.ReadToEnd();
            Assert.DoesNotContain("TOPLEVEL-SECRET", text);
            Assert.DoesNotContain("ROW-SECRET", text);
            Assert.DoesNotContain("token", text, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal("ann", content.PluginSettings["obs"]["accounts"]![0]!["user"]!.GetValue<string>());
        Assert.Equal("TOPLEVEL-SECRET", values["password"]!.GetValue<string>());
    }

    [Fact]
    public void A_newer_format_is_refused()
    {
        using var archive = new ZipArchive(new MemoryStream(Write()), ZipArchiveMode.Read);
        var manifest = new StreamReader(archive.GetEntry("manifest.json")!.Open()).ReadToEnd().Replace("\"formatVersion\": 1", "\"formatVersion\": 99");
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true)) Add(zip, "manifest.json", manifest);

        var ex = Assert.Throws<InvalidDataException>(() => BackupFile.Read(buffer.ToArray()));
        Assert.Contains("newer version", ex.Message);
    }

    [Fact]
    public void Unknown_and_path_like_entries_are_ignored()
    {
        var bytes = Rebuild(Write(), zip =>
        {
            Add(zip, "profiles/../evil.json", "{}");
            Add(zip, "profiles/a/b.json", "{}");
            Add(zip, "plugin-settings/..json", "{}");
            Add(zip, "languages/tr.json", "{}");
            Add(zip, "random.txt", "x");
        });

        var read = BackupFile.Read(bytes);

        Assert.Equal(2, read.Content.Profiles.Count);
        Assert.Single(read.Content.PluginSettings);
        Assert.Single(read.Content.LanguagePacks);
    }

    [Fact]
    public void An_invalid_profile_refuses_the_whole_file()
    {
        var bytes = Rebuild(Write(), zip => Add(zip, "profiles/bad.json", "{\"id\":\"bad\"}"));

        Assert.Throws<InvalidDataException>(() => BackupFile.Read(bytes));
    }

    [Fact]
    public void A_profile_whose_id_differs_from_its_entry_name_is_refused()
    {
        var json = JsonSerializer.Serialize(Sample("p9"), ProtocolJson.Options);
        var bytes = Rebuild(Write(), zip => Add(zip, "profiles/p8.json", json));

        Assert.Throws<InvalidDataException>(() => BackupFile.Read(bytes));
    }

    [Fact]
    public void Too_many_entries_and_a_large_entry_are_refused()
    {
        var many = Rebuild(Write(), zip => { for (var i = 0; i < BackupFile.MaxEntries; i++) Add(zip, $"x{i}.txt", ""); });
        Assert.Throws<InvalidDataException>(() => BackupFile.Read(many));

        var big = Rebuild(Write(), zip =>
        {
            using var stream = zip.CreateEntry("user-variables.json", CompressionLevel.Optimal).Open();
            var chunk = new byte[1024 * 1024];
            for (var i = 0; i < 65; i++) stream.Write(chunk);
        });
        Assert.Throws<InvalidDataException>(() => BackupFile.Read(big));
    }

    [Fact]
    public void Something_that_is_not_a_backup_is_refused()
    {
        Assert.Throws<InvalidDataException>(() => BackupFile.Read("not a zip"u8.ToArray()));
        var plain = Rebuild(Write(), _ => { });
        using var onlyOther = new MemoryStream();
        using (var zip = new ZipArchive(onlyOther, ZipArchiveMode.Create, true)) Add(zip, "profile.json", "{}");
        Assert.Throws<InvalidDataException>(() => BackupFile.Read(onlyOther.ToArray()));
        Assert.NotEmpty(plain);
    }
}
