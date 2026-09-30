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
    private readonly string _offPath = Path.Combine(dataDir, "plugin-permissions-off.json");
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
            var off = Read(_offPath);
            if (off.Remove(pluginId)) Write(off, _offPath);
        }
    }

    /// <summary>The approved permissions the person switched off for this plugin. They stay approved (switching one back on asks
    /// nothing), the plugin just runs without them.</summary>
    public IReadOnlyCollection<string> SwitchedOff(string pluginId) => Read(_offPath).GetValueOrDefault(pluginId) ?? [];

    public void SetSwitchedOff(string pluginId, string permission, bool off)
    {
        lock (_lock)
        {
            var all = Read(_offPath);
            var list = (all.GetValueOrDefault(pluginId) ?? []).Where(p => !p.Equals(permission, StringComparison.OrdinalIgnoreCase)).ToList();
            if (off) list.Add(permission);
            if (list.Count == 0) all.Remove(pluginId);
            else all[pluginId] = [.. list];
            Write(all, _offPath);
        }
    }

    private Dictionary<string, string[]> Read(string? path = null)
    {
        path ??= _path;
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path)) ?? []
                : [];
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return []; // an unreadable file grants nothing
        }
    }

    private void Write(Dictionary<string, string[]> all, string? path = null)
    {
        path ??= _path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
}
