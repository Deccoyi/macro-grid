using System.Text.Json;
using MacroGrid.Protocol;

namespace MacroGrid.Core.Preferences;

/// <summary>Persists the single <see cref="AppPreferences"/> record as one JSON file. Write-then-rename,
/// same crash-safety as ProfileStore/DeviceStore.</summary>
public sealed class PreferencesStore
{
    private static readonly JsonSerializerOptions FileJson = new(ProtocolJson.Options) { WriteIndented = true };

    private readonly string _path;
    private readonly Lock _lock = new();
    private AppPreferences _preferences = new();

    public PreferencesStore(string dataDir)
    {
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "preferences.json");
        Load();
    }

    public AppPreferences Get()
    {
        lock (_lock) return _preferences;
    }

    public void Save(AppPreferences preferences)
    {
        // The whole write-then-rename must be under the lock, not just the final field assignment —
        // two PUTs arriving close together (e.g. a toggle firing twice, or two windows saving near-
        // simultaneously) both target the same ".tmp" path, and the second one's File.Move/WriteAllText
        // throws IOException ("used by another process") if it races the first.
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(preferences, FileJson);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
            _preferences = preferences;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            var preferences = JsonSerializer.Deserialize<AppPreferences>(File.ReadAllText(_path), FileJson);
            if (preferences is not null) _preferences = preferences;
        }
        catch (JsonException)
        {
            File.Move(_path, _path + ".broken", overwrite: true);
        }
    }
}
