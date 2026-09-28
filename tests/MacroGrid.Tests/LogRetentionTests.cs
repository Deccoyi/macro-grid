using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class LogRetentionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "mg-logs-" + Guid.NewGuid().ToString("N"));
    private static readonly DateOnly Today = new(2026, 9, 26);

    public LogRetentionTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(DateOnly day, long bytes = 10)
    {
        var path = Path.Combine(_dir, LogRetention.FileName(day));
        using (var stream = File.Create(path)) stream.SetLength(bytes);
        return path;
    }

    [Fact]
    public void Files_older_than_the_limit_are_deleted()
    {
        var oldest = Write(Today.AddDays(-LogRetention.MaxAgeDays));
        var lastKept = Write(Today.AddDays(-(LogRetention.MaxAgeDays - 1)));
        var today = Write(Today);

        var deleted = LogRetention.Prune(_dir, Today);

        Assert.Equal([Path.GetFileName(oldest)], deleted);
        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(lastKept));
        Assert.True(File.Exists(today));
    }

    [Fact]
    public void The_oldest_files_go_first_when_the_total_is_too_big()
    {
        var size = LogRetention.MaxFileBytes;
        var d3 = Write(Today.AddDays(-3), size);
        var d2 = Write(Today.AddDays(-2), size);
        var d1 = Write(Today.AddDays(-1), size);
        var d0 = Write(Today, size);

        LogRetention.Prune(_dir, Today);

        // Four full files are exactly the total limit, so nothing has to go yet.
        Assert.True(File.Exists(d3));

        var d4 = Write(Today.AddDays(-4), size);
        LogRetention.Prune(_dir, Today);

        Assert.False(File.Exists(d4));
        Assert.True(File.Exists(d3));
        Assert.True(File.Exists(d2));
        Assert.True(File.Exists(d1));
        Assert.True(File.Exists(d0));
    }

    [Fact]
    public void Todays_file_is_never_deleted_even_when_it_alone_is_too_big()
    {
        var today = Write(Today, LogRetention.MaxTotalBytes + 1);

        LogRetention.Prune(_dir, Today);

        Assert.True(File.Exists(today));
    }

    [Fact]
    public void Other_files_in_the_folder_are_left_alone()
    {
        var other = Path.Combine(_dir, "notes.log");
        File.WriteAllText(other, "keep");
        var odd = Path.Combine(_dir, "server-crash.log");
        File.WriteAllText(odd, "keep");
        Write(Today.AddDays(-100));

        LogRetention.Prune(_dir, Today);

        Assert.True(File.Exists(other));
        Assert.True(File.Exists(odd));
    }

    [Fact]
    public void A_missing_folder_is_not_an_error() =>
        Assert.Empty(LogRetention.Prune(Path.Combine(_dir, "missing"), Today));

    [Theory]
    [InlineData("server-2026-09-26.log", true)]
    [InlineData("SERVER-2026-09-26.LOG", true)]
    [InlineData("server-2026-13-01.log", false)]
    [InlineData("server-crash.log", false)]
    [InlineData("client-2026-09-26.log", false)]
    public void Only_daily_log_names_are_recognized(string name, bool expected) =>
        Assert.Equal(expected, LogRetention.TryParseDay(name, out _));
}
