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
    private static byte[] Fill(int count, byte fill) => Enumerable.Repeat(fill, count).ToArray();
    private static byte[] Be32(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    public static byte[] Png(int padding = 16, byte fill = 1, int width = 1, int height = 1) =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, .. "IHDR"u8.ToArray(), .. Be32(width), .. Be32(height), 8, 2, 0, 0, 0, 0, 0, 0, 0, .. Fill(padding, fill)];

    /// <summary>A JPEG whose frame header (SOF0) comes after one other segment, so the marker walk is used.</summary>
    public static byte[] Jpeg(int padding = 16, int width = 1, int height = 1, bool withFrame = true) =>
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x04, 0x4A, 0x46,
        .. (withFrame ? new byte[] { 0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, 0x00 } : []),
        0xFF, 0xDA, .. Fill(padding, 2),
    ];

    public static byte[] Gif(int padding = 16, int frames = 1, int width = 1, int height = 1, bool trailer = true)
    {
        var bytes = new List<byte>("GIF89a"u8.ToArray()) { (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0 };
        for (var left = padding; left > 0;)
        {
            var chunk = Math.Min(left, 255);
            bytes.AddRange([0x21, 0xFE, (byte)chunk, .. Fill(chunk, 3), 0]);
            left -= chunk;
        }
        for (var i = 0; i < frames; i++) bytes.AddRange([0x2C, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0x02, 0x01, 0x00, 0x00]);
        if (trailer) bytes.Add(0x3B);
        return [.. bytes];
    }

    /// <summary>A WebP with a VP8X header (canvas size) followed by filler.</summary>
    public static byte[] Webp(int padding = 16, int width = 1, int height = 1) =>
    [
        .. "RIFF"u8.ToArray(), 0, 0, 0, 0, .. "WEBP"u8.ToArray(), .. "VP8X"u8.ToArray(), 10, 0, 0, 0, 0, 0, 0, 0,
        (byte)(width - 1), (byte)((width - 1) >> 8), (byte)((width - 1) >> 16), (byte)(height - 1), (byte)((height - 1) >> 8), (byte)((height - 1) >> 16),
        .. Fill(padding, 4),
    ];

    public static string Url(byte[] bytes, string type = "png") => $"data:image/{type};base64,{Convert.ToBase64String(bytes)}";

    public static string PngUrl(int padding = 16, byte fill = 1) => Url(Png(padding, fill));
}
