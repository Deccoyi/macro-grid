using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class UpdateStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Starts_empty()
    {
        Assert.Equal(new UpdateState(), new UpdateStateStore(_dir).Get());
    }

    [Fact]
    public void Saved_state_survives_reload()
    {
        var when = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);
        new UpdateStateStore(_dir).Update(s => s with
        {
            LastCheckUtc = when, ETag = "\"abc\"", FeedJson = "[]", SnoozedUntilUtc = when.AddHours(24), SkippedVersion = "0.3.0-alpha", NotifiedVersion = "0.3.1",
        });

        var state = new UpdateStateStore(_dir).Get();

        Assert.Equal(when, state.LastCheckUtc);
        Assert.Equal("\"abc\"", state.ETag);
        Assert.Equal("[]", state.FeedJson);
        Assert.Equal(when.AddHours(24), state.SnoozedUntilUtc);
        Assert.Equal("0.3.0-alpha", state.SkippedVersion);
        Assert.Equal("0.3.1", state.NotifiedVersion);
        Assert.False(File.Exists(Path.Combine(_dir, "update-state.json.tmp")));
    }

    [Fact]
    public void Update_applies_changes_one_after_another()
    {
        var store = new UpdateStateStore(_dir);

        store.Update(s => s with { ETag = "one" });
        var result = store.Update(s => s with { SkippedVersion = "2" });

        Assert.Equal("one", result.ETag);
        Assert.Equal("2", store.Get().SkippedVersion);
    }

    [Fact]
    public void Unreadable_file_is_moved_aside_and_the_state_starts_empty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "update-state.json"), "{ not json");

        var store = new UpdateStateStore(_dir);

        Assert.Equal(new UpdateState(), store.Get());
        Assert.True(File.Exists(Path.Combine(_dir, "update-state.json.broken")));
    }
}
