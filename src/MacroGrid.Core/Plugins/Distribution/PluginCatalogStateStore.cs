using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>What this PC remembers about one of the two signed catalog files.</summary>
public sealed record CatalogFileState
{
    /// <summary>The envelope as fetched. It is verified again every time the file is loaded.</summary>
    public string? Envelope { get; init; }

    /// <summary>The highest sequence ever accepted for this file. Kept even when the envelope is dropped, so an older file is still refused.</summary>
    public long Sequence { get; init; }

    public string? ETag { get; init; }

    /// <summary>The address <see cref="ETag"/> came from: an ETag is only sent back to that address.</summary>
    public string? ETagUrl { get; init; }

    /// <summary>The last successful check (a 304 counts).</summary>
    public DateTimeOffset? CheckedUtc { get; init; }

    public DateTimeOffset? NextCheckUtc { get; init; }

    public int Failures { get; init; }
}

public sealed record PluginCatalogState
{
    public CatalogFileState Index { get; init; } = new();
    public CatalogFileState Revoked { get; init; } = new();

    /// <summary>The API is not asked before this time (its hourly quota is used up or nearly so).</summary>
    public DateTimeOffset? ApiNotBeforeUtc { get; init; }

    public DateTimeOffset? CreatedUtc { get; init; }
}

/// <summary>Persists <see cref="PluginCatalogState"/> as <c>plugin-catalog-state.json</c>: write-then-rename like the update state; an unreadable
/// file is moved to <c>.broken</c> and the catalog starts from scratch.</summary>
public sealed class PluginCatalogStateStore
{
    private static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly Lock _lock = new();
    private PluginCatalogState _state = new();

    public PluginCatalogStateStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "plugin-catalog-state.json");
        Load();
    }

    public PluginCatalogState Get()
    {
        lock (_lock) return _state;
    }

    public PluginCatalogState Update(Func<PluginCatalogState, PluginCatalogState> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (_lock)
        {
            var next = change(_state);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(next, FileJson));
            File.Move(tmp, _path, overwrite: true);
            _state = next;
            return next;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var state = JsonSerializer.Deserialize<PluginCatalogState>(File.ReadAllText(_path), FileJson);
            if (state is not null) _state = state;
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
    }
}
