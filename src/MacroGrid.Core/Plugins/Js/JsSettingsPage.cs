using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>The plugin's settings page: the fields the script declared, stored in <c>settings.json</c> in its folder.
/// The value of a <see cref="SettingFieldKind.Password"/> field (also inside the rows of a list field) is written protected
/// (<see cref="ProtectedPrefix"/> + the host's secret protector) and handed back plain by <see cref="Load"/>, so the script
/// and the API see it as before; the API blanks it for the editor. A plain value from an older save still loads and is protected
/// by the next save. A value that cannot be unprotected here (another Windows user or PC) loads as an empty string and
/// <paramref name="onUnreadable"/> gets the field key; <paramref name="onSaved"/> runs after a save.</summary>
internal sealed class JsSettingsPage(string dataDirectory, IReadOnlyList<SettingField> fields, IPluginSecrets? secrets = null, Action<string>? onUnreadable = null, Action? onSaved = null) : IPluginSettingsPage
{
    /// <summary>Marks a stored password value as protected.</summary>
    public const string ProtectedPrefix = "mgp1:";

    /// <summary>A JavaScript plugin has no other way to write a file, so what it can keep is capped.</summary>
    public const int MaxBytes = 64 * 1024;

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
        Transform(fields, values, Unprotect);
        return values;
    }

    private string Unprotect(string key, string stored)
    {
        if (!stored.StartsWith(ProtectedPrefix, StringComparison.Ordinal)) return stored;
        string? plain = null;
        try { plain = secrets?.Unprotect(stored[ProtectedPrefix.Length..]); }
        catch (InvalidOperationException) { /* no protector here: unreadable */ }
        if (plain is null) onUnreadable?.Invoke(key);
        return plain ?? "";
    }

    /// <summary>Runs <paramref name="convert"/> on every non-empty string value of a password field, nested list rows included.</summary>
    private static void Transform(IReadOnlyList<SettingField> fields, JsonObject values, Func<string, string, string> convert)
    {
        foreach (var field in fields)
        {
            if (field.Kind == SettingFieldKind.Password)
            {
                if (values[field.Key] is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0)
                    values[field.Key] = convert(field.Key, text);
            }
            else if (field.Kind == SettingFieldKind.List && field.ItemFields is { Length: > 0 } itemFields && values[field.Key] is JsonArray rows)
            {
                foreach (var row in rows)
                    if (row is JsonObject rowValues) Transform(itemFields, rowValues, convert);
            }
        }
    }

    public void Save(JsonObject values)
    {
        // Only keys the plugin declared are kept, so a caller cannot store arbitrary data in the plugin's folder.
        var kept = new JsonObject();
        foreach (var field in fields)
            if (values[field.Key] is { } value) kept[field.Key] = value.DeepClone();
        // Always protect: the values come from Load (plain) or from the person, never raw from the file.
        Transform(fields, kept, (_, plain) => ProtectedPrefix + (secrets?.Protect(plain)
            ?? throw new InvalidOperationException("This host cannot protect a password field, so it was not saved.")));

        var json = kept.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxBytes)
            throw new InvalidOperationException($"The settings are too large to save (the limit is {MaxBytes / 1024} KB).");

        lock (_lock)
        {
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(_path, json);
        }
        onSaved?.Invoke();
    }
}
