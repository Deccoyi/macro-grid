using System.Text.Json;
using System.Text.Json.Serialization;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>Where an installed plugin came from — a local folder, the official catalog, an added third-party
/// source, or a pasted single-plugin link. Drives the Installed tab's badge and the "update available" check
/// (phases 3-5); recorded starting with the official source (phase 2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PluginTrust>))]
public enum PluginTrust
{
    Local,
    Official,
    ThirdParty,
}

public sealed record PluginInstallOrigin(string SourceUrl, string Version, PluginTrust Trust);

/// <summary>
/// Persists where each installed plugin came from, in <c>%AppData%\MacroGrid\plugin-installs.json</c>
/// (<c>{ "&lt;id&gt;": { "sourceUrl", "version", "trust" } }</c>, see the plugin distribution plan). A plugin
/// installed from a local folder (the only way before this feature existed) simply has no entry here.
/// </summary>
public sealed class PluginInstallOriginStore(string dataDir)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly Lock _lock = new();
    private string FilePath => Path.Combine(dataDir, "plugin-installs.json");

    public PluginInstallOrigin? Get(string pluginId)
    {
        lock (_lock)
            return Read().GetValueOrDefault(pluginId);
    }

    public void Set(string pluginId, PluginInstallOrigin origin)
    {
        lock (_lock)
        {
            var all = Read();
            all[pluginId] = origin;
            Write(all);
        }
    }

    public void Remove(string pluginId)
    {
        lock (_lock)
        {
            var all = Read();
            if (all.Remove(pluginId)) Write(all);
        }
    }

    private Dictionary<string, PluginInstallOrigin> Read()
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, PluginInstallOrigin>>(File.ReadAllText(FilePath), Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private void Write(Dictionary<string, PluginInstallOrigin> all)
    {
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(all, Json));
    }
}
