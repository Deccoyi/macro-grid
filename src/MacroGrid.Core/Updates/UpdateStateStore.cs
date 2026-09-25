using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroGrid.Core.Updates;

/// <summary>Persists the single <see cref="UpdateState"/> as <c>update-state.json</c>. Write-then-rename like
/// <see cref="Preferences.PreferencesStore"/>; an unreadable file is moved to <c>.broken</c> and the updater starts from scratch.</summary>
public sealed class UpdateStateStore
{
    private static readonly JsonSerializerOptions FileJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _path;
    private readonly Lock _lock = new();
    private UpdateState _state = new();

    public UpdateStateStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "update-state.json");
        Load();
    }

    public UpdateState Get()
    {
        lock (_lock) return _state;
    }

    /// <summary>Applies <paramref name="change"/> to the current state and saves the result, all under one lock so concurrent changes do not lose each other.</summary>
    public UpdateState Update(Func<UpdateState, UpdateState> change)
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
            var state = JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(_path), FileJson);
            if (state is not null) _state = state;
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
    }
}
