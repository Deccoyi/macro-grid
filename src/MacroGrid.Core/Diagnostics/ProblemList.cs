namespace MacroGrid.Core.Diagnostics;

public enum ProblemSeverity { Info, Warning, Error }

/// <summary>Where in a profile a problem happened, so the editor can take the person there. Ids only: the editor looks the names up.</summary>
public sealed record ProblemTarget(string ProfileId, string? PageId = null, string? WidgetId = null, string? Event = null, int? ActionIndex = null);

/// <summary>One line of the editor's Error List: the same message from the same source counted, not repeated.</summary>
public sealed record Problem(
    string Id, string Source, string SourceName, ProblemSeverity Severity, string Code, string Message,
    int Count, DateTimeOffset FirstAt, DateTimeOffset LastAt)
{
    /// <summary>Set by a plugin that wants to take the line back later (<see cref="ProblemList.ResolveKey"/>); not shown.</summary>
    public string? Key { get; init; }

    /// <summary>The widget (and event or action) the problem belongs to, when it belongs to one.</summary>
    public ProblemTarget? Target { get; init; }
}

/// <summary>Codes of the problems the server itself reports. The editor's own checks use their own codes.</summary>
public static class ProblemCodes
{
    /// <summary>A plugin asked for keyboard input and the rules refused it (nothing was sent).</summary>
    public const string InputRefused = "P100";
    /// <summary>A plugin tried to type a blocked command and was switched off.</summary>
    public const string InputBlocked = "P101";
    /// <summary>A network call of a JavaScript plugin was refused by the rules (the address is not what was approved, it is Macro Grid itself, or a header is not allowed).</summary>
    public const string NetworkRefused = "P102";
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
    /// <summary>A custom widget in a plugin's manifest was refused (a bad file, size or setting); the rest of the plugin works.</summary>
    public const string WidgetInvalid = "P140";
    /// <summary>A custom widget's request to its plugin failed or ran too long.</summary>
    public const string WidgetCallFailed = "P141";
    /// <summary>Events a plugin pushed to its widgets were dropped (too large or too many).</summary>
    public const string WidgetEventDropped = "P142";
    /// <summary>A custom widget reported an error from its own code.</summary>
    public const string WidgetScriptError = "P143";
    /// <summary>An official plugin version was switched off by the official safety list. It does not run until it is updated or removed.</summary>
    public const string Revoked = "P111";
    /// <summary>An installed plugin version was withdrawn by its publisher. It keeps running; the person is asked to update or remove it.</summary>
    public const string Withdrawn = "P112";
    /// <summary>The official safety list could not be checked for a long time. Plugins keep running.</summary>
    public const string CatalogStale = "P113";
    /// <summary>A press ran an action whose type is not available (its plugin was removed or is switched off). Cleared when a plugin loads.</summary>
    public const string ActionMissing = "P131";
    /// <summary>A plugin reported a problem itself (<c>IPluginDiagnostics</c>).</summary>
    public const string PluginReported = "P150";
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

    public void Report(string source, string sourceName, ProblemSeverity severity, string code, string message) =>
        Report(source, sourceName, severity, code, message, key: null, maxPerSourceAndCode: int.MaxValue);

    /// <summary>Adds the line, or counts it again. With a <paramref name="key"/> the line of that key is replaced (an upsert), so a problem that
    /// changes does not pile up. When the source already has <paramref name="maxPerSourceAndCode"/> different lines with this code, a new one is
    /// dropped (false); a line that is already there (same key, or same text) is updated and never counts against the cap again.</summary>
    public bool Report(
        string source, string sourceName, ProblemSeverity severity, string code, string message, string? key, int maxPerSourceAndCode, ProblemTarget? target = null)
    {
        message = Clean(message);
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            var index = key is not null
                ? _items.FindIndex(p => p.Source == source && p.Code == code && p.Key == key)
                : _items.FindIndex(p => p.Source == source && p.Code == code && p.Message == message && p.Severity == severity && p.Key is null);
            if (index >= 0)
            {
                var existing = _items[index];
                _items.RemoveAt(index);
                var same = existing.Message == message && existing.Severity == severity;
                _items.Insert(0, existing with { Message = message, Severity = severity, Count = same ? existing.Count + 1 : 1, LastAt = now, SourceName = sourceName, Target = target });
            }
            else
            {
                if (_items.Count(p => p.Source == source && p.Code == code) >= maxPerSourceAndCode) return false;
                _items.Insert(0, new Problem(Guid.NewGuid().ToString("N"), source, sourceName, severity, code, message, 1, now, now) { Key = key, Target = target });
                if (_items.Count > MaxEntries) _items.RemoveRange(MaxEntries, _items.Count - MaxEntries);
            }
            _version++;
        }
        Changed?.Invoke();
        return true;
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

    /// <summary>Removes the lines a plugin reported with this key.</summary>
    public void ResolveKey(string source, string key)
    {
        lock (_lock)
        {
            if (_items.RemoveAll(p => p.Source == source && p.Key == key) == 0) return;
            _version++;
        }
        Changed?.Invoke();
    }

    /// <summary>Removes every line of one source and code (a plugin clearing what it reported).</summary>
    public void ClearCode(string source, string code) => Resolve(source, code);

    private static string Clean(string message) => PlainText.Clean(message, MaxMessageLength);
}
