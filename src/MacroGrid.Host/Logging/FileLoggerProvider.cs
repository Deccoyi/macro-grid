using System.Collections.Concurrent;

namespace MacroGrid.Host.Logging;

/// <summary>Minimal daily log file writer; a WinExe has no console, so this is where users look when something breaks.</summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _dir;
    private readonly BlockingCollection<string> _lines = new(boundedCapacity: 10_000);
    private readonly Thread _writer;

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
                File.AppendAllText(Path.Combine(_dir, $"server-{DateTime.Now:yyyy-MM-dd}.log"), line + Environment.NewLine);
            }
            catch (IOException)
            {
                // Logging must never take the server down.
            }
        }
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
