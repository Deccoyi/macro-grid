using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class CrashMarkerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mg-crashmarker-" + Guid.NewGuid().ToString("N"));
    private string Logs => Path.Combine(_root, "logs");
    private string Crashes => Path.Combine(_root, "crashes");

    public CrashMarkerTests() => Directory.CreateDirectory(Logs);

    public void Dispose() => Directory.Delete(_root, true);

    private static readonly DateTimeOffset Now = new(2026, 10, 29, 14, 3, 12, 345, TimeSpan.FromHours(3));

    private static CrashMarker Marker(DateTimeOffset? at = null, CrashPhase phase = CrashPhase.Running, string details = "boom") => new(
        1, at ?? Now, phase, true, "0.5.0", "Windows", ".NET", 3723, "System.InvalidOperationException", details,
        [new CrashPlugin("a.b", "1.0", "Loaded")], "server-2026-10-29.log", 12);

    private static void Put(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), content);
    }

    private sealed class ThrowingException : Exception
    {
        public override string ToString() => throw new InvalidOperationException("no text");
    }

    [Fact]
    public void From_fills_every_field_and_cuts_a_huge_exception_text()
    {
        var marker = CrashMarker.From(new InvalidOperationException(new string('x', 50_000)), CrashPhase.Starting, Now, "0.5.0", TimeSpan.FromSeconds(3723.9), null, Logs);

        Assert.Equal(1, marker.Format);
        Assert.Equal(Now, marker.CrashedAt);
        Assert.Equal(CrashPhase.Starting, marker.Phase);
        Assert.True(marker.IsTerminating);
        Assert.Equal("0.5.0", marker.MacroGrid);
        Assert.False(string.IsNullOrEmpty(marker.Os));
        Assert.False(string.IsNullOrEmpty(marker.Runtime));
        Assert.Equal(3723, marker.UptimeSeconds);
        Assert.Equal("System.InvalidOperationException", marker.ExceptionType);
        Assert.Null(marker.Plugins);
        Assert.Equal(CrashMarkers.MaxDetailsLength, marker.Details.Length);
        Assert.EndsWith("...", marker.Details);
    }

    [Fact]
    public void From_gives_the_type_name_only_when_the_exception_text_throws()
    {
        var marker = CrashMarker.From(new ThrowingException(), CrashPhase.Running, Now, "1", TimeSpan.Zero, [], Logs);

        Assert.Equal(typeof(ThrowingException).FullName, marker.Details);
    }

    [Fact]
    public void The_log_file_and_its_length_come_from_the_log_of_that_day()
    {
        File.WriteAllText(Path.Combine(Logs, "server-2026-10-29.log"), "12345");

        var with = CrashMarker.From(new Exception("x"), CrashPhase.Running, Now, "1", TimeSpan.Zero, [], Logs);
        var without = CrashMarker.From(new Exception("x"), CrashPhase.Running, Now.AddDays(1), "1", TimeSpan.Zero, [], Logs);

        Assert.Equal("server-2026-10-29.log", with.LogFile);
        Assert.Equal(5, with.LogLength);
        Assert.Null(without.LogFile);
        Assert.Equal(0, without.LogLength);
    }

    [Fact]
    public void A_marker_survives_a_round_trip()
    {
        var marker = Marker();

        Assert.True(CrashMarkers.TryWrite(Crashes, marker));
        var set = CrashMarkers.Read(Crashes);

        var read = Assert.Single(set.Markers);
        Assert.Equal(marker with { Plugins = null }, read with { Plugins = null });
        Assert.Equal(marker.Plugins, read.Plugins);
        Assert.Empty(set.Unreadable);
        Assert.Single(set.Files);
        Assert.Contains("\"phase\": \"running\"", File.ReadAllText(set.Files[0]));
    }

    [Fact]
    public void Two_writes_in_the_same_millisecond_give_two_files()
    {
        Assert.True(CrashMarkers.TryWrite(Crashes, Marker()));
        Assert.True(CrashMarkers.TryWrite(Crashes, Marker()));
        Assert.True(CrashMarkers.TryWrite(Crashes, Marker()));

        var names = Directory.GetFiles(Crashes).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToList();
        var stamp = Now.LocalDateTime.ToString("yyyyMMdd-HHmmss-fff", System.Globalization.CultureInfo.InvariantCulture); // the file name uses local time, so it depends on the machine zone
        Assert.Equal([$"crash-{stamp}-2.json", $"crash-{stamp}-3.json", $"crash-{stamp}.json"], names);
    }

    [Fact]
    public void A_folder_that_cannot_be_created_returns_false_without_throwing()
    {
        File.WriteAllText(Path.Combine(_root, "blocker"), "a file, not a folder");

        Assert.False(CrashMarkers.TryWrite(Path.Combine(_root, "blocker", "crashes"), Marker()));
    }

    [Fact]
    public void Read_returns_the_newest_first_reads_at_most_ten_and_lists_all_files()
    {
        for (var i = 0; i < 12; i++) Assert.True(CrashMarkers.TryWrite(Crashes, Marker(Now.AddMinutes(i), details: "d" + i)));
        // Same millisecond: the suffixed file is the newer one.
        Assert.True(CrashMarkers.TryWrite(Crashes, Marker(Now.AddMinutes(11), details: "d11b")));

        var set = CrashMarkers.Read(Crashes);

        Assert.Equal(10, set.Markers.Count);
        Assert.Equal(["d11b", "d11", "d10", "d9", "d8", "d7", "d6", "d5", "d4", "d3"], set.Markers.Select(m => m.Details));
        Assert.Equal(13, set.Files.Count);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("{ \"format\": 2, \"crashedAt\": \"2026-10-29T14:03:12.345+03:00\" }")]
    [InlineData("{}")]
    public void An_unreadable_marker_is_counted_and_kept_in_the_file_list(string content)
    {
        Put(Crashes, "crash-20261029-140312-345.json", content);

        var set = CrashMarkers.Read(Crashes);

        Assert.Empty(set.Markers);
        var time = Assert.Single(set.Unreadable);
        Assert.Equal(new DateTime(2026, 10, 29, 14, 3, 12, 345), time!.Value.DateTime);
        Assert.Single(set.Files);
    }

    [Fact]
    public void A_marker_over_128_KiB_is_unreadable()
    {
        Put(Crashes, "crash-20261029-140312-345.json", new string(' ', (int)CrashMarkers.MaxFileBytes + 1));

        var set = CrashMarkers.Read(Crashes);

        Assert.Empty(set.Markers);
        Assert.Single(set.Unreadable);
    }

    [Fact]
    public void Files_not_named_like_a_marker_are_ignored()
    {
        Put(Crashes, "notes.json", "x");
        Put(Crashes, "crash-today.json", "x");
        Put(Crashes, "crash-20261029-140312-345.json.bak", "x");

        var set = CrashMarkers.Read(Crashes);

        Assert.Empty(set.Files);
        Assert.Empty(set.Unreadable);
    }

    [Fact]
    public void A_missing_folder_gives_an_empty_set()
    {
        Assert.Empty(CrashMarkers.Read(Path.Combine(_root, "nothing")).Files);
    }

    [Fact]
    public void Delete_ignores_a_locked_or_missing_file()
    {
        Assert.True(CrashMarkers.TryWrite(Crashes, Marker()));
        var path = Directory.GetFiles(Crashes).Single();
        var missing = Path.Combine(Crashes, "crash-gone.json");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            CrashMarkers.Delete([missing, path]);
        Assert.True(File.Exists(path));

        CrashMarkers.Delete([path]);
        Assert.False(File.Exists(path));
    }
}
