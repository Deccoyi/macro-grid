namespace MacroGrid.Core.Diagnostics;

public enum ProblemSeverity { Info, Warning, Error }

/// <summary>One line of the editor's Error List: the same message from the same source counted, not repeated.</summary>
public sealed record Problem(
    string Id, string Source, string SourceName, ProblemSeverity Severity, string Code, string Message,
    int Count, DateTimeOffset FirstAt, DateTimeOffset LastAt);

/// <summary>Codes of the problems the server itself reports. The editor's own checks use their own codes.</summary>
public static class ProblemCodes
{
    /// <summary>A plugin asked for keyboard input and the rules refused it (nothing was sent).</summary>
    public const string InputRefused = "P100";
    /// <summary>A plugin tried to type a blocked command and was switched off.</summary>
    public const string InputBlocked = "P101";
    /// <summary>A C# plugin was not loaded because it is not an official, unchanged plugin.</summary>
    public const string NotAllowed = "P110";
    /// <summary>A plugin could not be loaded or started.</summary>
    public const string LoadFailed = "P120";
    /// <summary>A plugin is not compatible with this Macro Grid.</summary>
    public const string Incompatible = "P121";
    /// <summary>A plugin was switched off after it failed.</summary>
    public const string SwitchedOff = "P122";
    /// <summary>One call of a plugin (an action, a timer) failed.</summary>
    public const string CallFailed = "P130";
}

/// <summary>
/// The problems the server has seen since the last "clear", newest first, at most <see cref="MaxEntries"/>. The same source, code and message
/// is one entry with a count (pressing a refused button five times is one line with 5), so the list stays short. In memory only: a restart
/// starts empty, and the log files hold the history. Messages are made of text a plugin can influence, so control characters are removed and the length is capped.
/// </summary>
public sealed class ProblemList
{
    public const int MaxEntries = 200;
    private const int MaxMessageLength = 300;

    private readonly Lock _lock = new();
    private readonly List<Problem> _items = [];
    private long _version;

    /// <summary>Raised after every change (outside the lock).</summary>
    public event Action? Changed;

    /// <summary>Goes up with every change, so a caller can tell cheaply whether it has the latest list.</summary>
    public long Version
    {
        get { lock (_lock) return _version; }
    }

    public void Report(string source, string sourceName, ProblemSeverity severity, string code, string message)
    {
        message = Clean(message);
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            var index = _items.FindIndex(p => p.Source == source && p.Code == code && p.Message == message && p.Severity == severity);
            if (index >= 0)
            {
                var existing = _items[index];
                _items.RemoveAt(index);
                _items.Insert(0, existing with { Count = existing.Count + 1, LastAt = now, SourceName = sourceName });
            }
            else
            {
                _items.Insert(0, new Problem(Guid.NewGuid().ToString("N"), source, sourceName, severity, code, message, 1, now, now));
                if (_items.Count > MaxEntries) _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
            }
            _version++;
        }
        Changed?.Invoke();
    }

    public IReadOnlyList<Problem> Snapshot()
    {
        lock (_lock) return [.. _items];
    }

    /// <summary>Removes the problems of one source (a fixed plugin) or, with no source, everything.</summary>
    public void Clear(string? source = null)
    {
        lock (_lock)
        {
            if (_items.RemoveAll(p => source is null || p.Source == source) == 0) return;
            _version++;
        }
        Changed?.Invoke();
    }

    /// <summary>Removes the entries of one source and code, for a problem that is gone (a plugin that loads now).</summary>
    public void Resolve(string source, params string[] codes)
    {
        lock (_lock)
        {
            if (_items.RemoveAll(p => p.Source == source && codes.Contains(p.Code)) == 0) return;
            _version++;
        }
        Changed?.Invoke();
    }

    private static string Clean(string message)
    {
        var chars = message.Where(c => !char.IsControl(c)).Take(MaxMessageLength).ToArray();
        return new string(chars);
    }
}
