using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>A plugin's storage call went over a limit. The message is safe to show to the script.</summary>
public sealed class StorageLimitException(string message) : Exception(message);

/// <summary>
/// The small key/value store of a JavaScript plugin with the <c>storage</c> permission: <c>storage.json</c> in the plugin's own folder
/// (<c>{ "formatVersion": 1, "values": { key: json text } }</c>). The script never names a path. It is read once, kept in memory and
/// written at most every two seconds after a change and when the plugin stops, through a temporary file that then replaces the old one.
/// Plain text, like <c>settings.json</c>: it is not a place for secrets.
/// </summary>
internal sealed partial class JsStorage : IDisposable
{
    public const int MaxKeys = 64;
    public const int MaxKeyLength = 64;
    public const int MaxValueBytes = 16 * 1024;
    public const int MaxTotalBytes = 256 * 1024;
    private const int FormatVersion = 1;

    private static readonly TimeSpan WriteDelay = TimeSpan.FromSeconds(2);

    private readonly string _path;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private readonly SortedDictionary<string, string> _values = new(StringComparer.Ordinal);
    private Timer? _timer;
    private bool _dirty;
    private bool _pendingWrite;
    private bool _disposed;

    public JsStorage(string dataDirectory, ILogger logger)
    {
        _path = Path.Combine(dataDirectory, "storage.json");
        _logger = logger;
        Load();
    }

    public string? Get(string key)
    {
        lock (_lock) return _values.TryGetValue(key, out var value) ? value : null;
    }

    public IReadOnlyList<string> Keys()
    {
        lock (_lock) return [.. _values.Keys];
    }

    /// <summary>Stores <paramref name="json"/> (already JSON text) under <paramref name="key"/>.</summary>
    public void Set(string key, string json)
    {
        if (!KeyPattern().IsMatch(key))
            throw new StorageLimitException($"A storage key is 1-{MaxKeyLength} characters: letters, digits, '.', '_' and '-'.");
        var bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > MaxValueBytes)
            throw new StorageLimitException($"A stored value can be at most {MaxValueBytes / 1024} KB.");

        lock (_lock)
        {
            var isNew = !_values.ContainsKey(key);
            if (isNew && _values.Count >= MaxKeys)
                throw new StorageLimitException($"A plugin can store at most {MaxKeys} keys.");
            var total = _values.Where(p => p.Key != key).Sum(p => Encoding.UTF8.GetByteCount(p.Value)) + bytes;
            if (total > MaxTotalBytes)
                throw new StorageLimitException($"A plugin can store at most {MaxTotalBytes / 1024} KB in total.");
            _values[key] = json;
            MarkDirty();
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_values.Remove(key)) MarkDirty();
        }
    }

    /// <summary>Writes what is waiting, now. Called when the plugin stops.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _timer?.Dispose();
            _timer = null;
            if (_dirty) Write();
        }
    }

    // Called with the lock held. One write is scheduled per burst of changes, so a script that sets a value on every tick still reaches the disk every two seconds.
    private void MarkDirty()
    {
        _dirty = true;
        if (_pendingWrite || _disposed) return;
        _pendingWrite = true;
        _timer?.Dispose();
        _timer = new Timer(_ => { lock (_lock) { _pendingWrite = false; if (!_disposed && _dirty) Write(); } }, null, WriteDelay, Timeout.InfiniteTimeSpan);
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            using var doc = JsonDocument.Parse(File.ReadAllText(_path));
            if (!doc.RootElement.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Object) return;
            foreach (var property in values.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String && KeyPattern().IsMatch(property.Name) && _values.Count < MaxKeys)
                    _values[property.Name] = property.Value.GetString()!;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning("A plugin's storage file could not be read and starts empty: {Error}", ex.Message);
            _values.Clear();
        }
    }

    // Called with the lock held.
    private void Write()
    {
        _dirty = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var json = JsonSerializer.Serialize(new { formatVersion = FormatVersion, values = _values }, new JsonSerializerOptions { WriteIndented = true });
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _dirty = true;
            _logger.LogWarning("A plugin's storage file could not be written: {Error}", ex.Message);
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9._\-]{1,64}$")]
    private static partial Regex KeyPattern();
}
