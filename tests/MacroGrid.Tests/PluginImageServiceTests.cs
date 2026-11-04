using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins.Images;

namespace MacroGrid.Tests;

public sealed class PluginImageServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-imgsvc-" + Guid.NewGuid().ToString("N"));
    private readonly ManualTime _time = new();
    private readonly ProblemList _problems = new();
    private readonly CapturedLog<PluginImageService> _log = new();
    private readonly PluginImageService _service;
    private readonly RecordingImageSource _source = new();

    public PluginImageServiceTests()
    {
        Directory.CreateDirectory(_dir);
        _service = new PluginImageService(_log, _problems, _time);
        _service.Attach("vm", "Voice plugin", _dir, _source);
    }

    public void Dispose()
    {
        _service.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private void Tick(TimeSpan? by = null) => _time.Advance(by ?? TimeSpan.Zero);

    [Fact]
    public void A_miss_asks_the_plugin_once_and_a_second_miss_does_not_ask_again()
    {
        Assert.Null(_service.Resolve("plugin-image:vm/voice:1"));
        Tick();
        Assert.Null(_service.Resolve("plugin-image:vm/voice:1"));
        Tick(TimeSpan.FromSeconds(5));

        Assert.Equal([["voice:1"]], _source.Calls);
    }

    [Fact]
    public void Requests_go_out_in_batches_of_20_and_not_faster_than_every_500_ms()
    {
        for (var i = 0; i < 25; i++) _service.Resolve($"plugin-image:vm/id{i}");
        Tick();
        Assert.Single(_source.Calls);
        Assert.Equal(20, _source.Calls[0].Length);

        Tick(TimeSpan.FromMilliseconds(499));
        Assert.Single(_source.Calls);

        Tick(TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, _source.Calls.Count);
        Assert.Equal(5, _source.Calls[1].Length);
        Assert.Equal(25, _source.AllIds.Distinct().Count());
    }

    [Fact]
    public void A_late_single_request_waits_for_the_interval_since_the_last_call()
    {
        _service.Resolve("plugin-image:vm/a");
        Tick();
        _service.Resolve("plugin-image:vm/b");
        Tick(TimeSpan.FromMilliseconds(100));
        Assert.Single(_source.Calls);

        Tick(TimeSpan.FromMilliseconds(400));

        Assert.Equal([["a"], ["b"]], _source.Calls);
    }

    [Fact]
    public void At_most_200_requests_are_open_at_once()
    {
        for (var i = 0; i < 300; i++) _service.Resolve($"plugin-image:vm/id{i}");
        for (var round = 0; round < 30; round++) Tick(TimeSpan.FromMilliseconds(500));

        Assert.Equal(200, _source.AllIds.Distinct().Count());
    }

    [Fact]
    public void An_unanswered_request_is_asked_again_after_ten_minutes()
    {
        _service.Resolve("plugin-image:vm/a");
        Tick();
        Tick(TimeSpan.FromMinutes(9));
        _service.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromSeconds(1));
        Assert.Single(_source.Calls);

        Tick(TimeSpan.FromMinutes(2));
        _service.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromSeconds(1));

        Assert.Equal(2, _source.Calls.Count);
    }

    [Fact]
    public void A_requested_picture_is_stored_served_and_announced()
    {
        var announced = new List<(string Plugin, string[]? Ids)>();
        _service.ImagesChanged += (plugin, ids) => announced.Add((plugin, ids?.ToArray()));
        _service.Resolve("plugin-image:vm/voice:1");
        Tick();

        Assert.True(_service.Put("vm", "voice:1", TestPictures.PngUrl()));

        Assert.Equal(TestPictures.PngUrl(), _service.Resolve("plugin-image:vm/voice:1"));
        var change = Assert.Single(announced);
        Assert.Equal("vm", change.Plugin);
        Assert.Equal(["voice:1"], change.Ids);
        var served = _service.TryGetBytes("vm", "voice:1");
        Assert.Equal("image/png", served!.Value.MediaType);
        Assert.Equal(TestPictures.Png(), served.Value.Bytes);
        Assert.Empty(_problems.Snapshot());
    }

    [Fact]
    public void An_answer_nobody_asked_for_is_refused_with_one_problem_line_per_plugin()
    {
        Assert.False(_service.Put("vm", "voice:unasked", TestPictures.PngUrl()));
        Assert.False(_service.Put("vm", "voice:other", TestPictures.PngUrl()));

        Assert.Null(_service.Resolve("plugin-image:vm/voice:unasked") ?? null);
        var line = Assert.Single(_problems.Snapshot());
        Assert.Equal(ProblemCodes.ImageRefused, line.Code);
        Assert.Equal(2, line.Count);
        Assert.Equal("vm", line.Source);
        Assert.DoesNotContain("voice", line.Message);
        Assert.DoesNotContain("base64", line.Message);
    }

    [Fact]
    public void An_answer_is_refused_after_the_ten_minute_window()
    {
        _service.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromMinutes(11));

        Assert.False(_service.Put("vm", "a", TestPictures.PngUrl()));
    }

    [Fact]
    public void An_answer_shortly_after_the_first_one_is_still_accepted_inside_the_window()
    {
        _service.Resolve("plugin-image:vm/a");
        Tick();
        Assert.True(_service.Put("vm", "a", TestPictures.PngUrl(fill: 1)));

        Tick(TimeSpan.FromMinutes(5));

        Assert.True(_service.Put("vm", "a", TestPictures.PngUrl(fill: 2)));
        Assert.Equal(TestPictures.PngUrl(fill: 2), _service.TryGetDataUrl("vm", "a"));
    }

    [Theory]
    [InlineData("svg")]
    [InlineData("mismatch")]
    [InlineData("big")]
    [InlineData("text")]
    public void A_bad_picture_for_a_requested_id_is_refused_and_not_stored(string kind)
    {
        _service.Resolve("plugin-image:vm/a");
        Tick();
        var data = kind switch
        {
            "svg" => "data:image/svg+xml;base64," + Convert.ToBase64String("<svg/>"u8.ToArray()),
            "mismatch" => TestPictures.Url(TestPictures.Png(), "gif"),
            "big" => TestPictures.PngUrl(300 * 1024),
            _ => "data:text/plain;base64,aGk=",
        };

        Assert.False(_service.Put("vm", "a", data));

        Assert.Null(_service.TryGetDataUrl("vm", "a"));
        Assert.Single(_problems.Snapshot());
    }

    [Fact]
    public void An_invalid_image_id_is_refused_even_if_requested_by_name()
    {
        _service.RequestImage("vm", "bad id");
        Assert.False(_service.Put("vm", "bad id", TestPictures.PngUrl()));
        Assert.False(_service.Put("vm", "meme:1＠abc", TestPictures.PngUrl()));
    }

    [Fact]
    public void A_picture_is_served_while_the_plugin_is_not_running_and_requests_wait_for_its_return()
    {
        _service.Resolve("plugin-image:vm/cached");
        Tick();
        _service.Put("vm", "cached", TestPictures.PngUrl());
        _service.Detach("vm");

        Assert.Equal(TestPictures.PngUrl(), _service.Resolve("plugin-image:vm/cached"));
        Assert.Null(_service.Resolve("plugin-image:vm/missing"));
        Tick(TimeSpan.FromSeconds(2));
        Assert.Single(_source.Calls); // only the first request, from before it stopped

        var again = new RecordingImageSource();
        _service.Attach("vm", "Voice plugin", _dir, again);
        Tick();

        Assert.Equal([["missing"]], again.Calls);
    }

    [Fact]
    public void A_plugin_that_never_ran_still_serves_what_an_earlier_run_cached()
    {
        _service.Resolve("plugin-image:vm/cached");
        Tick();
        _service.Put("vm", "cached", TestPictures.PngUrl());

        using var restarted = new PluginImageService(_log, _problems, _time);
        restarted.Attach("vm", "Voice plugin", _dir, null);

        Assert.Equal(TestPictures.PngUrl(), restarted.TryGetDataUrl("vm", "cached"));
    }

    [Fact]
    public void Attaching_a_new_plugin_folder_announces_all_its_pictures_once()
    {
        var announced = new List<IReadOnlyCollection<string>?>();
        _service.ImagesChanged += (_, ids) => announced.Add(ids);

        _service.Attach("other", "Other", Path.Combine(_dir, "other"), null);
        _service.Attach("other", "Other", Path.Combine(_dir, "other"), new RecordingImageSource());

        Assert.Equal([null], announced);
    }

    [Fact]
    public void Forgetting_a_plugin_deletes_its_cache_and_stops_serving()
    {
        _service.Resolve("plugin-image:vm/a");
        Tick();
        _service.Put("vm", "a", TestPictures.PngUrl());
        Assert.True(Directory.Exists(Path.Combine(_dir, PluginImageCache.FolderName)));

        _service.Forget("vm");

        Assert.False(Directory.Exists(Path.Combine(_dir, PluginImageCache.FolderName)));
        Assert.False(_service.IsKnown("vm"));
        Assert.Null(_service.Resolve("plugin-image:vm/a"));
        Assert.False(_service.Put("vm", "a", TestPictures.PngUrl()));
    }

    [Fact]
    public void An_unknown_plugin_or_a_malformed_reference_asks_nobody()
    {
        Assert.Null(_service.Resolve("plugin-image:nobody/a"));
        Assert.Null(_service.Resolve("plugin-image:vm"));
        Assert.Null(_service.Resolve("plugin-image:vm/bad id"));
        Tick(TimeSpan.FromSeconds(2));

        Assert.Empty(_source.Calls);
    }

    [Fact]
    public void A_plugin_that_throws_while_asked_is_logged_without_any_id()
    {
        var throwing = new ThrowingSource();
        _service.Attach("vm", "Voice plugin", _dir, throwing);

        _service.Resolve("plugin-image:vm/secret-name-42");
        Tick();

        Assert.Equal(1, throwing.Calls);
        Assert.DoesNotContain(_log.Lines, l => l.Contains("secret-name-42"));
        Assert.Contains(_log.Lines, l => l.Contains("InvalidOperationException"));
    }

    [Fact]
    public void Logs_and_problem_lines_never_hold_an_image_id_or_picture_data()
    {
        var url = TestPictures.PngUrl(fill: 7);
        _service.Resolve("plugin-image:vm/private-voice-name");
        Tick();
        _service.Put("vm", "private-voice-name", url);
        _service.Put("vm", "private-other-name", url);
        _service.Put("vm", "private-voice-name", "data:image/svg+xml;base64,PHN2Zy8+");

        var everything = string.Join("\n", _log.Lines.Concat(_problems.Snapshot().Select(p => p.Message)));
        Assert.DoesNotContain("private-", everything);
        Assert.DoesNotContain("base64", everything);
        Assert.DoesNotContain(url[30..], everything);
    }

    // ---- not fetched unless used; refresh; invalidate ----

    private void Cached(string id, int fill = 1, string? version = null)
    {
        _service.RequestImage("vm", id);
        Tick();
        Assert.True(_service.Put("vm", id, TestPictures.PngUrl(fill: (byte)fill), version));
        _source.Calls.Clear();
    }

    [Fact]
    public void Looking_at_a_cached_or_missing_picture_without_using_it_requests_nothing()
    {
        Assert.Null(_service.TryGetBytes("vm", "voice:1"));
        Assert.Null(_service.TryGetDataUrl("vm", "voice:1"));
        Tick(TimeSpan.FromSeconds(5));

        Assert.Empty(_source.Calls);
    }

    [Fact]
    public void A_picture_younger_than_a_week_is_never_refreshed()
    {
        Cached("a");
        Tick(TimeSpan.FromDays(6));

        Assert.NotNull(_service.Resolve("plugin-image:vm/a"));
        Tick(TimeSpan.FromSeconds(2));

        Assert.Empty(_source.Calls);
    }

    [Fact]
    public void An_old_picture_is_served_at_once_and_asked_for_again_once_per_week()
    {
        Cached("a", fill: 1);
        Tick(TimeSpan.FromDays(8));

        Assert.Equal(TestPictures.PngUrl(fill: 1), _service.Resolve("plugin-image:vm/a"));
        Tick();
        Assert.Equal([["a"]], _source.Calls);
        Assert.Equal(TestPictures.PngUrl(fill: 1), _service.Resolve("plugin-image:vm/a")); // still the old one until the answer comes
        Tick(TimeSpan.FromSeconds(2));
        Assert.Single(_source.Calls);

        var announced = new List<string[]?>();
        _service.ImagesChanged += (_, ids) => announced.Add(ids?.ToArray());
        Assert.True(_service.Put("vm", "a", TestPictures.PngUrl(fill: 2)));

        Assert.Equal(TestPictures.PngUrl(fill: 2), _service.Resolve("plugin-image:vm/a"));
        Assert.Equal(["a"], Assert.Single(announced));
        // the new picture is fresh and the id was refreshed a moment ago: nothing more is asked
        Tick(TimeSpan.FromDays(6));
        _service.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromSeconds(2));
        Assert.Single(_source.Calls);
    }

    [Fact]
    public void An_id_that_was_refreshed_is_not_asked_again_within_a_week_even_if_the_answer_never_came()
    {
        Cached("a");
        Tick(TimeSpan.FromDays(8));
        _service.Resolve("plugin-image:vm/a");
        Tick();
        Tick(TimeSpan.FromMinutes(11)); // the open request ran out

        _service.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromSeconds(2));

        Assert.Single(_source.Calls);
    }

    [Fact]
    public void At_most_ten_refreshes_per_hour_per_plugin()
    {
        for (var i = 0; i < 12; i++) Cached("id" + i);
        Tick(TimeSpan.FromDays(8));

        for (var i = 0; i < 12; i++) _service.Resolve($"plugin-image:vm/id{i}");
        for (var round = 0; round < 5; round++) Tick(TimeSpan.FromMilliseconds(500));
        Assert.Equal(10, _source.AllIds.Distinct().Count());

        Tick(TimeSpan.FromHours(1));
        _service.Resolve("plugin-image:vm/id10");
        _service.Resolve("plugin-image:vm/id11");
        Tick(TimeSpan.FromSeconds(2));
        Assert.Equal(12, _source.AllIds.Distinct().Count());
    }

    [Fact]
    public void Refreshes_share_the_batching()
    {
        for (var i = 0; i < 3; i++) Cached("id" + i);
        Tick(TimeSpan.FromDays(8));
        for (var i = 0; i < 3; i++) _service.Resolve($"plugin-image:vm/id{i}");

        Tick();

        Assert.Equal([["id0", "id1", "id2"]], _source.Calls);
    }

    [Fact]
    public void An_old_picture_of_a_stopped_plugin_is_still_served_and_asked_for_when_it_returns()
    {
        Cached("a");
        _service.Detach("vm");
        Tick(TimeSpan.FromDays(9));

        Assert.NotNull(_service.Resolve("plugin-image:vm/a"));
        Tick(TimeSpan.FromSeconds(2));
        Assert.Empty(_source.Calls);

        var back = new RecordingImageSource();
        _service.Attach("vm", "Voice plugin", _dir, back);
        Tick();

        Assert.Equal([["a"]], back.Calls);
    }

    [Fact]
    public void The_time_of_a_picture_survives_a_restart()
    {
        Cached("a", version: "v7");
        Tick(TimeSpan.FromDays(3));
        using var restarted = new PluginImageService(_log, _problems, _time);
        restarted.Attach("vm", "Voice plugin", _dir, _source);
        restarted.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromSeconds(2));
        Assert.Empty(_source.Calls); // 3 days old: fresh

        Tick(TimeSpan.FromDays(5));
        restarted.Resolve("plugin-image:vm/a");
        Tick(TimeSpan.FromSeconds(2));
        Assert.Equal([["a"]], _source.Calls);
    }

    [Fact]
    public void Invalidate_asks_again_for_a_cached_picture_unless_the_version_is_the_same()
    {
        Cached("a", version: "v1");

        _service.Invalidate("vm", "a", "v1");
        Tick(TimeSpan.FromSeconds(1));
        Assert.Empty(_source.Calls);

        _service.Invalidate("vm", "a", "v2");
        Tick(TimeSpan.FromSeconds(1));
        Assert.Equal([["a"]], _source.Calls);
        Assert.True(_service.Put("vm", "a", TestPictures.PngUrl(fill: 5), "v2"));

        _service.Invalidate("vm", "a", "v2");
        Tick(TimeSpan.FromSeconds(1));
        Assert.Single(_source.Calls);

        _service.Invalidate("vm", "a"); // no version: always
        Tick(TimeSpan.FromSeconds(1));
        Assert.Equal(2, _source.Calls.Count);
    }

    [Fact]
    public void Invalidate_of_a_picture_that_is_not_cached_or_not_valid_does_nothing()
    {
        _service.Invalidate("vm", "never-cached");
        _service.Invalidate("vm", "bad id");
        _service.Invalidate("nobody", "a");
        Tick(TimeSpan.FromSeconds(2));

        Assert.Empty(_source.Calls);
    }

    [Fact]
    public void A_bad_version_is_refused_and_a_64_character_one_is_kept()
    {
        _service.RequestImage("vm", "a");
        Assert.False(_service.Put("vm", "a", TestPictures.PngUrl(), ""));
        Assert.False(_service.Put("vm", "a", TestPictures.PngUrl(), "line\nbreak"));
        Assert.False(_service.Put("vm", "a", TestPictures.PngUrl(), new string('v', 65)));
        Assert.True(_service.Put("vm", "a", TestPictures.PngUrl(), new string('v', 64)));
    }

    private sealed class ThrowingSource : MacroGrid.Plugin.Abstractions.IPluginImageSource
    {
        public int Calls { get; private set; }

        public void RequestImages(IReadOnlyList<string> imageIds)
        {
            Calls++;
            throw new InvalidOperationException("boom " + string.Join(",", imageIds));
        }
    }
}
