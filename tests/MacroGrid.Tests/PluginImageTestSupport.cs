using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Tests;

/// <summary>A clock the test moves by hand. A timer fires inside <see cref="Advance"/> when its time has come (a due time of zero fires on <c>Advance(TimeSpan.Zero)</c>).</summary>
internal sealed class ManualTime : TimeProvider
{
    private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly List<ManualTimer> _timers = [];

    public override DateTimeOffset GetUtcNow() => _now;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan by)
    {
        var target = _now + by;
        while (_timers.Where(t => t.Due is not null && t.Due <= target).OrderBy(t => t.Due).FirstOrDefault() is { } next)
        {
            if (next.Due > _now) _now = next.Due.Value;
            next.Fire();
        }
        _now = target;
    }

    private sealed class ManualTimer(ManualTime owner, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; private set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Due = dueTime == Timeout.InfiniteTimeSpan ? null : owner._now + dueTime;
            return true;
        }

        public void Fire()
        {
            Due = null;
            callback(state);
        }

        public void Dispose() => Due = null;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}

internal sealed class RecordingImageSource : IPluginImageSource
{
    public List<string[]> Calls { get; } = [];
    public IEnumerable<string> AllIds => Calls.SelectMany(c => c);

    public void RequestImages(IReadOnlyList<string> imageIds) => Calls.Add([.. imageIds]);
}

/// <summary>Keeps every formatted log line (including the exception text), so a test can prove nothing sensitive was logged.</summary>
internal sealed class CapturedLog<T> : ILogger<T>
{
    public List<string> Lines { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Lines.Add(formatter(state, exception) + (exception is null ? "" : " " + exception));
}

internal static class TestPictures
{
    public static byte[] Png(int padding = 16, byte fill = 1) => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. Enumerable.Repeat(fill, padding)];
    public static byte[] Jpeg(int padding = 16) => [0xFF, 0xD8, 0xFF, 0xE0, .. Enumerable.Repeat((byte)2, padding)];
    public static byte[] Gif(int padding = 16) => [.. "GIF89a"u8.ToArray(), .. Enumerable.Repeat((byte)3, padding)];
    public static byte[] Webp(int padding = 16) => [.. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray(), .. Enumerable.Repeat((byte)4, padding)];

    public static string Url(byte[] bytes, string type = "png") => $"data:image/{type};base64,{Convert.ToBase64String(bytes)}";

    public static string PngUrl(int padding = 16, byte fill = 1) => Url(Png(padding, fill));
}
