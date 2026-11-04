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
    public void Exactly_256_KB_passes_and_one_byte_more_does_not()
    {
        var room = PluginImageValidator.MaxDecodedBytes - TestPictures.Png(0).Length;
        Assert.Equal(256 * 1024, TestPictures.Png(room).Length);

        Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(room)), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.TooLarge, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(room + 1)), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(200 * 1024)), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.TooLarge, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(257 * 1024)), out _, out _));
    }

    [Fact]
    public void A_picture_of_each_type_at_1024_x_1024_passes_and_a_larger_side_does_not()
    {
        (byte[] Ok, byte[] Wide, string Type)[] cases =
        [
            (TestPictures.Png(width: 1024, height: 1024), TestPictures.Png(width: 1025, height: 1), "png"),
            (TestPictures.Jpeg(width: 1024, height: 1024), TestPictures.Jpeg(width: 1025, height: 1), "jpeg"),
            (TestPictures.Gif(width: 1024, height: 1024), TestPictures.Gif(width: 1025, height: 1), "gif"),
            (TestPictures.Webp(width: 1024, height: 1024), TestPictures.Webp(width: 1025, height: 1), "webp"),
        ];
        foreach (var (ok, wide, type) in cases)
        {
            Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url(ok, type), out _, out _));
            Assert.Equal(PluginImageValidator.Failure.TooManyPixels, PluginImageValidator.TryValidate(TestPictures.Url(wide, type), out _, out _));
        }
    }

    [Fact]
    public void A_decode_bomb_header_is_refused_without_reading_further()
    {
        Assert.Equal(PluginImageValidator.Failure.TooManyPixels, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(width: 30000, height: 30000)), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.TooManyPixels, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(width: int.MaxValue, height: int.MaxValue)), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.TooManyPixels, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Png(width: 0, height: 5)), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.TooManyPixels, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Webp(width: 20000, height: 20000), "webp"), out _, out _));
    }

    [Fact]
    public void A_header_that_cannot_be_read_is_refused()
    {
        var png = TestPictures.Png();
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(png[..20]), out _, out _));
        var notFirst = (byte[])png.Clone();
        notFirst[12] = (byte)'X';
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(notFirst), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Jpeg(withFrame: false), "jpeg"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url([0xFF, 0xD8, 0xFF, 0xC0, 0x00], "jpeg"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Webp()[..24], "webp"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Gif()[..10], "gif"), out _, out _));
    }

    [Fact]
    public void A_gif_frame_larger_than_the_screen_counts_and_a_huge_or_empty_frame_is_refused()
    {
        var gif = TestPictures.Gif(width: 1, height: 1);
        int frame = Array.IndexOf(gif, (byte)0x2C, 13);
        var big = (byte[])gif.Clone();
        big[frame + 5] = 0xD0; big[frame + 6] = 0x07; big[frame + 7] = 0xD0; big[frame + 8] = 0x07; // 2000 x 2000
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(big, "gif"), out _, out _));

        var huge = (byte[])gif.Clone();
        huge[frame + 5] = 0xFF; huge[frame + 6] = 0xFF; huge[frame + 7] = 0xFF; huge[frame + 8] = 0xFF; // 65535 x 65535
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(huge, "gif"), out _, out _));

        var empty = (byte[])gif.Clone();
        empty[frame + 5] = 0; empty[frame + 6] = 0;
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(empty, "gif"), out _, out _));

        var offset = (byte[])gif.Clone();
        offset[frame + 1] = 0xFF; offset[frame + 2] = 0x03; // left 1023 + width 1 fits; left 1024 + 1 does not
        Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url(offset, "gif"), out _, out _));
        offset[frame + 1] = 0x00; offset[frame + 2] = 0x04;
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(offset, "gif"), out _, out _));
    }

    [Fact]
    public void A_jpeg_with_another_kind_of_frame_header_is_refused_even_if_a_normal_one_follows()
    {
        var jpeg = TestPictures.Jpeg();
        byte[] fake = [0xFF, 0xC9, 0x00, 0x0B, 0x08, 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x01, 0x11, 0x00];
        var bytes = jpeg[..8].Concat(fake).Concat(jpeg[8..]).ToArray();

        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(bytes, "jpeg"), out _, out _));
    }

    [Fact]
    public void An_animated_png_or_webp_is_refused()
    {
        var png = TestPictures.Png(0);
        byte[] actl = [0, 0, 0, 8, .. "acTL"u8.ToArray(), 0, 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url([.. png, .. actl]), out _, out _));
        byte[] idat = [0, 0, 0, 2, .. "IDAT"u8.ToArray(), 1, 2, 0, 0, 0, 0];
        Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url([.. png, .. idat, .. actl]), out _, out _)); // after the image data: not read

        var webp = TestPictures.Webp();
        webp[20] |= 0x02;
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(webp, "webp"), out _, out _));
    }

    [Fact]
    public void A_jpeg_marker_walk_stops_after_64_markers()
    {
        byte[] segment = [0xFF, 0xE1, 0x00, 0x02];
        var bytes = new List<byte> { 0xFF, 0xD8 };
        for (var i = 0; i < 70; i++) bytes.AddRange(segment);
        bytes.AddRange(TestPictures.Jpeg()[8..]);

        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url([.. bytes], "jpeg"), out _, out _));
    }

    [Fact]
    public void A_gif_is_limited_to_64_frames_and_must_have_sound_blocks()
    {
        Assert.Equal(PluginImageValidator.Failure.None, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Gif(frames: 64), "gif"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Gif(frames: 65), "gif"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Gif(trailer: false), "gif"), out _, out _));
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(TestPictures.Gif(frames: 0), "gif"), out _, out _));
        var unknownBlock = TestPictures.Gif()[..^1].Concat(new byte[] { 0x99, 0x3B }).ToArray();
        Assert.Equal(PluginImageValidator.Failure.Unreadable, PluginImageValidator.TryValidate(TestPictures.Url(unknownBlock, "gif"), out _, out _));
    }

    [Fact]
    public void The_webp_lossy_and_lossless_headers_are_read()
    {
        byte[] lossy = [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray(), .. "VP8 "u8.ToArray(), 10, 0, 0, 0, 0, 0, 0, 0x9D, 0x01, 0x2A, 0x00, 0x04, 0x00, 0x02];
        byte[] lossless = [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray(), .. "VP8L"u8.ToArray(), 10, 0, 0, 0, 0x2F, 0xFF, 0x03, 0x00, 0x00, 0, 0, 0, 0, 0];

        Assert.True(PluginImageValidator.TryReadSize(lossy, "webp", out var w, out var h));
        Assert.Equal((1024, 512), (w, h));
        Assert.True(PluginImageValidator.TryReadSize(lossless, "webp", out w, out h));
        Assert.Equal((1024, 1), (w, h));
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
        var file = Assert.Single(Directory.GetFiles(cache.Folder, "*.bin"));
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
        var cache = new PluginImageCache(_dir, maxBytes: 80);
        cache.Put("a", TestPictures.Png(0));
        cache.Put("b", TestPictures.Png(0));

        cache.Put("c", TestPictures.Png(0)); // 3 x 33 bytes > 80

        Assert.Null(cache.TryGet("a"));
        Assert.NotNull(cache.TryGet("b"));
        Assert.NotNull(cache.TryGet("c"));
        Assert.True(cache.TotalBytes <= 80);
    }

    [Fact]
    public void The_default_limits_are_64_MB_and_2000_files()
    {
        Assert.Equal(64L * 1024 * 1024, PluginImageCache.DefaultMaxBytes);
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
