using System.Collections.Concurrent;
using System.Text;
using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Host.Logging;

/// <summary>Minimal daily log file writer; a WinExe has no console, so this is where users look when something breaks.
/// Old files are removed and a day's file is capped (<see cref="LogRetention"/>), so logs never pile up on the PC.</summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly BlockingCollection<string> _lines = new(boundedCapacity: 10_000);
    private readonly Thread _writer;

    // Only touched by the writer thread.
    private DateOnly _day;
    private long _dayBytes;
    private bool _dayCapped;

    public FileLoggerProvider(string dir)
    {
        _dir = dir;
        Directory.CreateDirectory(dir);
        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "FileLogger" };
        _writer.Start();
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    public void Dispose()
    {
        _lines.CompleteAdding();
        _writer.Join(TimeSpan.FromSeconds(2));
    }

    private void Enqueue(string line) => _lines.TryAdd(line);

    private void WriteLoop()
    {
        foreach (var line in _lines.GetConsumingEnumerable())
        {
            try
            {
                var path = StartDayIfNeeded();
                if (_dayCapped) continue;

                var text = line + Environment.NewLine;
                if (_dayBytes + Encoding.UTF8.GetByteCount(text) > LogRetention.MaxFileBytes)
                {
                    _dayCapped = true;
                    text = $"{DateTime.Now:HH:mm:ss.fff} [WARN] FileLogger: Today's log reached its size limit; further lines are dropped until tomorrow.{Environment.NewLine}";
                }
                File.AppendAllText(path, text);
                _dayBytes += Encoding.UTF8.GetByteCount(text);
            }
            catch (IOException)
            {
                // Logging must never take the server down.
            }
        }
    }

    /// <summary>On the first line and on the first line of each new day: prunes old files and picks up the size of
    /// today's file (it may already exist from an earlier run today).</summary>
    private string StartDayIfNeeded()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var path = Path.Combine(_dir, LogRetention.FileName(today));
        if (today == _day) return path;

        _day = today;
        try
        {
            LogRetention.Prune(_dir, today);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Pruning is retried on the next day change or start.
        }
        _dayBytes = File.Exists(path) ? new FileInfo(path).Length : 0;
        _dayCapped = _dayBytes >= LogRetention.MaxFileBytes;
        return path;
    }

    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        private readonly string _category = category[(category.LastIndexOf('.') + 1)..];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel >= (category.StartsWith("Microsoft", StringComparison.Ordinal) ? LogLevel.Warning : LogLevel.Information);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var line = $"{DateTime.Now:HH:mm:ss.fff} [{logLevel.ToString()[..4].ToUpperInvariant()}] {_category}: {formatter(state, exception)}";
            if (exception is not null) line += Environment.NewLine + exception;
            provider.Enqueue(line);
        }
    }
}
