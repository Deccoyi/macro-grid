using System.Text.Json;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>A third-party multi-plugin repository the user added as a source (method 3 of the plugin
/// distribution plan). <see cref="Id"/> is <c>"&lt;owner&gt;/&lt;repo&gt;"</c>, lowercase — stable and
/// human-readable, so no separate id needs to be invented or persisted.</summary>
public sealed record PluginSource(string Id, string Owner, string Repo, string Name, DateTimeOffset AddedAt);

/// <summary>
/// Persists the list of added sources in <c>%AppData%\MacroGrid\plugin-sources.json</c>. The built-in official
/// source is never stored here — it is hard-coded (<see cref="PluginSourceUrls.OfficialOwner"/>/<see
/// cref="PluginSourceUrls.OfficialRepo"/>) and cannot be removed.
/// </summary>
public sealed class PluginSourceStore(string dataDir)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Lock _lock = new();
    private string FilePath => Path.Combine(dataDir, "plugin-sources.json");

    public IReadOnlyList<PluginSource> All()
    {
        lock (_lock)
            return Read();
    }

    public PluginSource? Find(string id)
    {
        lock (_lock)
            return Read().FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <returns>False when a source for this owner/repo is already saved (the caller should treat this as
    /// success, not an error — adding the same source twice is a no-op).</returns>
    public bool Add(PluginSource source)
    {
        lock (_lock)
        {
            var all = Read();
            if (all.Any(s => string.Equals(s.Id, source.Id, StringComparison.OrdinalIgnoreCase))) return false;
            Write([.. all, source]);
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_lock)
        {
            var all = Read();
            var kept = all.Where(s => !string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase)).ToList();
            if (kept.Count == all.Count) return false;
            Write(kept);
            return true;
        }
    }

    private List<PluginSource> Read()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<PluginSource>>(File.ReadAllText(FilePath), Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Write(List<PluginSource> sources)
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(sources, Json));
    }
}
