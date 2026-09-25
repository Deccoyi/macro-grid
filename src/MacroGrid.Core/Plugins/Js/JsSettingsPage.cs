using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>The plugin's settings page: the fields the script declared, stored in <c>settings.json</c> in its folder.</summary>
internal sealed class JsSettingsPage(string dataDirectory, IReadOnlyList<SettingField> fields) : IPluginSettingsPage
{
    private readonly string _path = Path.Combine(dataDirectory, "settings.json");
    private readonly Lock _lock = new();

    public IReadOnlyList<SettingField> Fields => fields;

    public JsonObject Load()
    {
        var values = new JsonObject();
        foreach (var field in fields)
            if (field.Default is not null) values[field.Key] = field.Default.DeepClone();

        lock (_lock)
        {
            try
            {
                if (File.Exists(_path) && JsonNode.Parse(File.ReadAllText(_path)) is JsonObject saved)
                    foreach (var (key, value) in saved) values[key] = value?.DeepClone();
            }
            catch (Exception ex) when (ex is JsonException or IOException) { /* fall back to the defaults */ }
        }
        return values;
    }

    public void Save(JsonObject values)
    {
        // Only keys the plugin declared are kept, so a caller cannot store arbitrary data in the plugin's folder.
        var kept = new JsonObject();
        foreach (var field in fields)
            if (values[field.Key] is { } value) kept[field.Key] = value.DeepClone();

        lock (_lock)
        {
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(_path, kept.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
