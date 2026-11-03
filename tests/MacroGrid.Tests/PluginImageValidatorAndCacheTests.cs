using MacroGrid.Core.Plugins.Images;

namespace MacroGrid.Tests;

public sealed class PluginImageValidatorAndCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-imgcache-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Theory]
    [InlineData("png")]
    [InlineData("jpeg")]
    [InlineData("gif")]
    [InlineData("webp")]
    public void Each_allowed_type_passes_when_the_bytes_match(string type)
    {
        var bytes = type switch { "png" => TestPictures.Png(), "jpeg" => TestPictures.Jpeg(), "gif" => TestPictures.Gif(), _ => TestPictures.Webp() };

        var result = PluginImageValidator.TryValidate(TestPictures.Url(bytes, type), out var decoded, out var mediaType);

        Assert.Equal(PluginImageValidator.Failure.None, result);
        Assert.Equal(bytes, decoded);
        Assert.Equal("image/" + type, mediaType);
    }

    [Fact]
    public void Svg_is_refused_even_when_the_content_looks_harmless()
    {
        var svg = "data:image/svg+xml;base64," + Convert.ToBase64String("<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray());
        Assert.Equal(PluginImageValidator.Failure.UnsupportedType, PluginImageValidator.TryValidate(svg, out _, out _));
    }

    [Fact]
    public void Magic_bytes_must_match_the_declared_type()
    {
        Assert.Equal(PluginImageValidator.Failure.ContentMismatch, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(), "jpeg"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.ContentMismatch, PluginImageValidator.TryValidate(TestPictures.Url("<svg/>"u8.ToArray(), "png"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.ContentMismatch, PluginImageValidator.TryValidate(TestPictures.Url("RIFF....WAVE"u8.ToArray(), "webp"), out _, out _));
    }

    [Fact]
    public void Exactly_64_KB_passes_and_one_byte_more_does_not()
    {
        var ok = TestPictures.Png(PluginImageValidator.MaxDecodedBytes - 8);
        var big = TestPictures.Png(PluginImageValidator.MaxDecodedBytes - 7);
        Assert.Equal(PluginImageValidator.MaxDecodedBytes, ok.Length);

        Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url(ok), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.TooLarge, PluginImageValidator.TryValidate(TestPictures.Url(big), out _, out _));
    }

    [Fact]
    public void A_huge_string_is_refused_without_decoding_it()
    {
        var huge = "data:image/png;base64," + new string('A', 5_000_000);
        Assert.Equal(PluginImageValidator.Failure.TooLarge, PluginImageValidator.TryValidate(huge, out _, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://example.test/a.png")]
    [InlineData("data:text/html;base64,PGI+")]
    [InlineData("data:image/png,rawbytes")]
    [InlineData("data:image/png;base64,")]
    [InlineData("data:image/png;base64,@@@@")]
    [InlineData("DATA:image/png;base64,iVBORw0KGgo=")]
    public void Anything_else_is_refused(string? value)
    {
        Assert.NotEqual(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(value, out _, out _));
    }

    [Theory]
    [InlineData("voice:abc-123", true)]
    [InlineData("sound:0123456789abcdef", true)]
    [InlineData("meme:12.3_x:4", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("a/b", false)]
    [InlineData("..\\x", false)]
    [InlineData("meme:1＠abc", false)] // the full-width @ is outside the allowed characters
    public void Image_ids_use_a_strict_character_set(string id, bool valid)
    {
        Assert.Equal(valid, PluginImageRef.IsValidImageId(id));
    }

    [Fact]
    public void An_id_of_128_characters_is_the_longest()
    {
        Assert.True(PluginImageRef.IsValidImageId(new string('a', 128)));
        Assert.False(PluginImageRef.IsValidImageId(new string('a', 129)));
    }

    [Theory]
    [InlineData("plugin-image:voicemod/voice:abc", true, "voicemod", "voice:abc")]
    [InlineData("plugin-image:a.b/c", true, "a.b", "c")]
    [InlineData("plugin-image:/c", false, "", "")]
    [InlineData("plugin-image:a", false, "", "")]
    [InlineData("plugin-image:a/", false, "", "")]
    [InlineData("plugin-image:a/b/c", false, "", "")]
    [InlineData("plugin-image:-a/b", false, "", "")]
    [InlineData("data:image/png;base64,AA==", false, "", "")]
    public void A_reference_parses_into_plugin_and_image(string value, bool valid, string plugin, string image)
    {
        Assert.Equal(valid, PluginImageRef.TryParse(value, out var p, out var i));
        Assert.Equal(plugin, p);
        Assert.Equal(image, i);
    }

    // ---- cache ----

    [Fact]
    public void A_stored_picture_reads_back_and_lives_in_a_hashed_file_name()
    {
        var cache = new PluginImageCache(_dir);
        var bytes = TestPictures.Png();

        Assert.True(cache.Put("voice:x", bytes));

        Assert.Equal(bytes, cache.TryGet("voice:x"));
        var file = Assert.Single(Directory.GetFiles(cache.Folder));
        Assert.EndsWith(".bin", file);
        Assert.DoesNotContain("voice", Path.GetFileName(file));
        Assert.Equal(PluginImageCache.FileNameFor("voice:x"), Path.GetFileName(file));
        Assert.Null(cache.TryGet("voice:other"));
    }

    [Fact]
    public void The_least_recently_used_picture_goes_first_when_the_file_limit_is_reached()
    {
        var time = new ManualTime();
        var cache = new PluginImageCache(_dir, time, maxFiles: 3);
        foreach (var id in new[] { "a", "b", "c" }) { cache.Put(id, TestPictures.Png()); time.Advance(TimeSpan.FromSeconds(1)); }
        cache.TryGet("a"); // a is fresh again, b is now the oldest

        cache.Put("d", TestPictures.Png());

        Assert.Equal(3, cache.Count);
        Assert.NotNull(cache.TryGet("a"));
        Assert.Null(cache.TryGet("b"));
        Assert.NotNull(cache.TryGet("c"));
        Assert.NotNull(cache.TryGet("d"));
        Assert.Equal(3, Directory.GetFiles(cache.Folder, "*.bin").Length);
    }

    [Fact]
    public void The_byte_limit_evicts_the_oldest_and_keeps_the_newest()
    {
        var cache = new PluginImageCache(_dir, maxBytes: 100);
        cache.Put("a", TestPictures.Png(42));
        cache.Put("b", TestPictures.Png(42));

        cache.Put("c", TestPictures.Png(42)); // 3 x 50 bytes > 100

        Assert.Null(cache.TryGet("a"));
        Assert.NotNull(cache.TryGet("b"));
        Assert.NotNull(cache.TryGet("c"));
        Assert.True(cache.TotalBytes <= 100);
    }

    [Fact]
    public void The_default_limits_are_20_MB_and_2000_files()
    {
        Assert.Equal(20L * 1024 * 1024, PluginImageCache.DefaultMaxBytes);
        Assert.Equal(2000, PluginImageCache.DefaultMaxFiles);
    }

    [Fact]
    public void The_order_survives_a_restart()
    {
        var first = new PluginImageCache(_dir, maxFiles: 2);
        first.Put("old", TestPictures.Png());
        File.SetLastWriteTimeUtc(Path.Combine(first.Folder, PluginImageCache.FileNameFor("old")), DateTime.UtcNow.AddHours(-2));
        first.Put("new", TestPictures.Png());

        var second = new PluginImageCache(_dir, maxFiles: 2);
        second.Put("newest", TestPictures.Png());

        Assert.Null(second.TryGet("old"));
        Assert.NotNull(second.TryGet("new"));
    }

    [Fact]
    public void A_file_that_vanished_is_forgotten()
    {
        var cache = new PluginImageCache(_dir);
        cache.Put("a", TestPictures.Png());
        File.Delete(Path.Combine(cache.Folder, PluginImageCache.FileNameFor("a")));

        Assert.Null(cache.TryGet("a"));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Clear_removes_the_folder()
    {
        var cache = new PluginImageCache(_dir);
        cache.Put("a", TestPictures.Png());

        cache.Clear();

        Assert.False(Directory.Exists(cache.Folder));
        Assert.Null(cache.TryGet("a"));
    }

    [Fact]
    public void A_repeated_read_does_not_touch_the_disk_again_and_a_put_replaces_what_is_served()
    {
        var cache = new PluginImageCache(_dir);
        cache.Put("a", TestPictures.Png(16, 1));
        var path = Path.Combine(cache.Folder, PluginImageCache.FileNameFor("a"));
        Assert.NotNull(cache.TryGet("a"));
        var stamp = File.GetLastWriteTimeUtc(path);
        File.SetLastWriteTimeUtc(path, stamp.AddHours(-1));

        Assert.NotNull(cache.TryGet("a"));
        Assert.Equal(stamp.AddHours(-1), File.GetLastWriteTimeUtc(path)); // not touched again within a minute

        cache.Put("a", TestPictures.Png(16, 2));
        Assert.Equal(TestPictures.Png(16, 2), cache.TryGet("a"));
    }

    [Fact]
    public void A_leftover_temp_file_is_removed_when_the_folder_is_read()
    {
        Directory.CreateDirectory(Path.Combine(_dir, PluginImageCache.FolderName));
        var temp = Path.Combine(_dir, PluginImageCache.FolderName, "x.bin.tmp");
        File.WriteAllBytes(temp, [1]);

        Assert.Equal(0, new PluginImageCache(_dir).Count);
        Assert.False(File.Exists(temp));
    }
}
