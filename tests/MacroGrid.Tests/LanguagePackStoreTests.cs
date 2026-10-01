using System.Text;
using MacroGrid.Core.Languages;

namespace MacroGrid.Tests;

public sealed class LanguagePackStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static string Pack(string tag, string name = "Deutsch", string strings = "\"app.loading\":\"Laden\"") =>
        $"{{\"meta\":{{\"format\":1,\"tag\":\"{tag}\",\"name\":\"{name}\",\"version\":3}},\"strings\":{{{strings}}}}}";

    [Theory]
    [InlineData("de")]
    [InlineData("pt-BR")]
    [InlineData("zh-Hant")]
    [InlineData("sr-Latn-RS")]
    public void IsValidTag_accepts_language_tags(string tag) => Assert.True(LanguagePackStore.IsValidTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("d")]
    [InlineData("tr")]
    [InlineData("en")]
    [InlineData("DE")]
    [InlineData("../de")]
    [InlineData("de/x")]
    [InlineData("de\\x")]
    [InlineData("de.json")]
    [InlineData("de.")]
    [InlineData("..")]
    [InlineData("de-")]
    [InlineData("de-x:y")]
    [InlineData("de-aaaaaaaaa")]
    [InlineData("aa-bbbbbbbb-cccccccc-dddddddd-eeeeeeee")]
    public void IsValidTag_refuses_everything_else(string? tag) => Assert.False(LanguagePackStore.IsValidTag(tag));

    [Fact]
    public void Save_then_read_gives_the_same_text_and_a_listing_with_a_cleaned_name()
    {
        var store = new LanguagePackStore(_dir);
        var json = Pack("de", "Deut\\u200Bsch\\u202E");

        store.Save("de", json);

        Assert.Equal(json, store.Read("de"));
        var listed = Assert.Single(store.List());
        Assert.Equal(new LanguagePackInfo("de", "Deutsch", 3), listed);
    }

    [Fact]
    public void Saving_a_tag_twice_replaces_the_same_file()
    {
        var store = new LanguagePackStore(_dir);
        store.Save("pt-BR", Pack("pt-BR"));
        store.Save("pt-BR", Pack("pt-BR", "Portugues"));

        Assert.Single(store.List());
        Assert.Single(Directory.GetFiles(Path.Combine(_dir, "languages")));
    }

    [Fact]
    public void Save_refuses_a_bad_tag_a_mismatch_and_wrong_shapes()
    {
        var store = new LanguagePackStore(_dir);

        Assert.Throws<LanguagePackException>(() => store.Save("../x", Pack("de")));
        Assert.Throws<LanguagePackException>(() => store.Save("tr", Pack("tr")));
        Assert.Throws<LanguagePackException>(() => store.Save("de", Pack("fr")));
        Assert.Throws<LanguagePackException>(() => store.Save("de", "not json"));
        Assert.Throws<LanguagePackException>(() => store.Save("de", "[]"));
        Assert.Throws<LanguagePackException>(() => store.Save("de", "{\"meta\":{\"tag\":\"de\"}}"));
        Assert.Throws<LanguagePackException>(() => store.Save("de", "{\"meta\":[],\"strings\":{}}"));
        Assert.Throws<LanguagePackException>(() => store.Save("de", Pack("de", strings: "\"a\":1")));
        Assert.Throws<LanguagePackException>(() => store.Save("de", Pack("de", strings: "\"a\":{\"b\":\"c\"}")));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Save_refuses_too_large_and_too_many()
    {
        var store = new LanguagePackStore(_dir);

        var huge = Pack("de", strings: "\"a\":\"" + new string('x', LanguagePackStore.MaxBytes) + "\"");
        Assert.Throws<LanguagePackException>(() => store.Save("de", huge));

        var many = string.Join(",", Enumerable.Range(0, LanguagePackStore.MaxEntries + 1).Select(i => $"\"k{i}\":\"v\""));
        Assert.Throws<LanguagePackException>(() => store.Save("de", Pack("de", strings: many)));

        var limit = string.Join(",", Enumerable.Range(0, LanguagePackStore.MaxEntries).Select(i => $"\"k{i}\":\"v\""));
        store.Save("de", Pack("de", strings: limit));
        Assert.NotNull(store.Read("de"));
    }

    [Fact]
    public void Save_refuses_a_twenty_first_pack_but_still_updates_an_installed_one()
    {
        var store = new LanguagePackStore(_dir);
        for (var i = 0; i < LanguagePackStore.MaxPacks; i++)
        {
            var tag = "x" + (char)('a' + i);
            store.Save(tag, Pack(tag));
        }

        Assert.Throws<LanguagePackException>(() => store.Save("de", Pack("de")));
        store.Save("xa", Pack("xa", "Again"));
        Assert.Equal(LanguagePackStore.MaxPacks, store.List().Count);
    }

    [Fact]
    public void Delete_removes_the_file_and_says_whether_there_was_one()
    {
        var store = new LanguagePackStore(_dir);
        store.Save("de", Pack("de"));

        Assert.True(store.Delete("de"));
        Assert.False(store.Delete("de"));
        Assert.False(store.Delete("../de"));
        Assert.Null(store.Read("de"));
    }

    [Fact]
    public void A_broken_file_on_disk_does_not_break_the_list()
    {
        var store = new LanguagePackStore(_dir);
        store.Save("de", Pack("de"));
        File.WriteAllText(Path.Combine(_dir, "languages", "fr.json"), "{ broken");
        File.WriteAllText(Path.Combine(_dir, "languages", "es.json"), Pack("it"));
        File.WriteAllText(Path.Combine(_dir, "languages", "pl.json"), "{\"meta\":{\"tag\":5},\"strings\":{}}");

        Assert.Equal(["de"], store.List().Select(p => p.Tag));
    }

    [Fact]
    public void Nothing_is_listed_before_the_first_pack()
    {
        Assert.Empty(new LanguagePackStore(_dir).List());
        Assert.Null(new LanguagePackStore(_dir).Read("de"));
    }
}

public sealed class LanguageCsvFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public LanguageCsvFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void ReadText_reads_utf8_with_or_without_a_byte_order_mark()
    {
        var plain = Path.Combine(_dir, "a.csv");
        var bom = Path.Combine(_dir, "b.csv");
        File.WriteAllText(plain, "key;ş", new UTF8Encoding(false));
        File.WriteAllText(bom, "key;ş", new UTF8Encoding(true));

        Assert.Equal("key;ş", LanguageCsvFile.ReadText(plain));
        Assert.EndsWith("key;ş", LanguageCsvFile.ReadText(bom));
    }

    [Fact]
    public void ReadText_refuses_a_large_file_before_reading_it_and_broken_bytes()
    {
        var large = Path.Combine(_dir, "large.csv");
        using (var stream = File.Create(large)) stream.SetLength(LanguageCsvFile.MaxImportBytes + 1);
        Assert.Throws<LanguagePackException>(() => LanguageCsvFile.ReadText(large));

        var broken = Path.Combine(_dir, "broken.csv");
        File.WriteAllBytes(broken, [0x6B, 0xFF, 0xFE, 0x6B]);
        Assert.Throws<LanguagePackException>(() => LanguageCsvFile.ReadText(broken));

        Assert.Throws<LanguagePackException>(() => LanguageCsvFile.ReadText(Path.Combine(_dir, "none.csv")));
    }

    [Fact]
    public void PrepareExport_adds_a_byte_order_mark_and_a_safe_csv_name()
    {
        var (name, bytes) = LanguageCsvFile.PrepareExport("a/b:c?", "ş");

        Assert.Equal("a_b_c_.csv", name);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("ş", Encoding.UTF8.GetString(bytes[3..]));
        Assert.Equal("language.csv", LanguageCsvFile.PrepareExport("  ", "x").FileName);
        Assert.Equal("de.csv", LanguageCsvFile.PrepareExport("de.csv", "x").FileName);
    }

    [Fact]
    public void PrepareExport_refuses_a_large_text_and_a_missing_one()
    {
        Assert.Throws<LanguagePackException>(() => LanguageCsvFile.PrepareExport("x", new string('a', LanguageCsvFile.MaxExportBytes + 1)));
        Assert.Throws<LanguagePackException>(() => LanguageCsvFile.PrepareExport("x", null));
        Assert.Throws<LanguagePackException>(() => LanguageCsvFile.PrepareExport("x", "bad\uD800"));
    }
}
