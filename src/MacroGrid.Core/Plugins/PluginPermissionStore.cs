using System.Text.Json;

namespace MacroGrid.Core.Plugins;

/// <summary>
/// Which permissions the user has approved for each JS plugin, kept in <c>plugin-permissions.json</c> next to the
/// other user data. An approval is for an exact permission set: if an updated plugin asks for more than was
/// approved, it waits for approval again instead of silently getting the new access.
/// </summary>
public sealed class PluginPermissionStore(string dataDir)
{
    private readonly string _path = Path.Combine(dataDir, "plugin-permissions.json");
    private readonly Lock _lock = new();

    public bool IsGranted(string pluginId, IEnumerable<string> declared)
    {
        var granted = Read().GetValueOrDefault(pluginId) ?? [];
        return declared.All(p => granted.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    public void Grant(string pluginId, IEnumerable<string> permissions)
    {
        lock (_lock)
        {
            var all = Read();
            all[pluginId] = [.. permissions];
            Write(all);
        }
    }

    public void Revoke(string pluginId)
    {
        lock (_lock)
        {
            var all = Read();
            if (all.Remove(pluginId)) Write(all);
        }
    }

    private Dictionary<string, string[]> Read()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(_path)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return []; // an unreadable file grants nothing
        }
    }

    private void Write(Dictionary<string, string[]> all)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
}
