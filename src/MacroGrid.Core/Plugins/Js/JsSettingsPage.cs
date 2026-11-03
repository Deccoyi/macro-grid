using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Backup;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Js;

/// <summary>The plugin's settings page: the fields the script declared, stored in <c>settings.json</c> in its folder.
/// The value of a <see cref="SettingFieldKind.Password"/> field (also inside the rows of a list field) is written protected
/// (<see cref="ProtectedPrefix"/> + the host's secret protector) and handed back plain by <see cref="Load"/>, so the script
/// and the API see it as before; the API blanks it for the editor. A plain value from an older save still loads and is protected
/// by the next save. A value that cannot be unprotected here (another Windows user or PC) loads as an empty string and
/// <paramref name="onUnreadable"/> gets the field key; <paramref name="onSaved"/> runs after a save.</summary>
internal sealed class JsSettingsPage(string dataDirectory, IReadOnlyList<SettingField> fields, IPluginSecrets? secrets = null, Action<string>? onUnreadable = null, Action? onSaved = null,
    Func<string, JsonObject, OptionsQuery, CancellationToken, Task<OptionsResult>>? options = null) : IPluginSettingsPage, IOptionsSearch
{
    /// <summary>Marks a stored password value as protected.</summary>
    public const string ProtectedPrefix = "mgp1:";

    /// <summary>A JavaScript plugin has no other way to write a file, so what it can keep is capped.</summary>
    public const int MaxBytes = 64 * 1024;

    private readonly string _path = Path.Combine(dataDirectory, "settings.json");
    private readonly Lock _lock = new();

    public IReadOnlyList<SettingField> Fields => fields;

    public Task<OptionsResult> GetOptionsAsync(string sourceId, JsonObject currentValues, CancellationToken cancellationToken) =>
        SearchOptionsAsync(sourceId, currentValues, new OptionsQuery("", 0, OptionsQuery.MaxLimit, null), cancellationToken);

    public Task<OptionsResult> SearchOptionsAsync(string sourceId, JsonObject currentValues, OptionsQuery query, CancellationToken cancellationToken) =>
        options is null ? Task.FromResult(JsOptions.Fail("This plugin does not provide dynamic options.")) : options(sourceId, currentValues, query, cancellationToken);

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
                {
                    MigratePlainValues(saved);
                    foreach (var (key, value) in saved) values[key] = value?.DeepClone();
                }
            }
            catch (Exception ex) when (ex is JsonException or IOException) { /* fall back to the defaults */ }
        }
        PluginSettingsSecrets.ForEachPassword(fields, values, (holder, field) =>
        {
            if (holder[field.Key] is JsonValue v && v.TryGetValue<string>(out var text) && text.Length > 0)
                holder[field.Key] = Unprotect(field.Key, text);
        });
        return values;
    }

    /// <summary>A plain value from an older save is protected and written back at the first load, so it does not stay on disk until the
    /// next save. Best effort: when it cannot be done the file stays as it was.</summary>
    private void MigratePlainValues(JsonObject saved)
    {
        try
        {
            if (!ProtectPasswords(saved, onlyPlain: true)) return;
            File.WriteAllText(_path, saved.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is InvalidOperationException or CryptographicException or IOException or UnauthorizedAccessException) { }
    }

    /// <summary>Protects every non-empty password in place. A password that is not a string is refused when saving
    /// (it would be stored unprotected) and skipped when migrating. Returns whether anything changed.</summary>
    private bool ProtectPasswords(JsonObject values, bool onlyPlain)
    {
        var changed = false;
        PluginSettingsSecrets.ForEachPassword(fields, values, (holder, field) =>
        {
            if (holder[field.Key] is not { } node) return;
            if (node is not JsonValue v || !v.TryGetValue<string>(out var text))
            {
                if (!onlyPlain) throw new InvalidOperationException($"The setting '{field.Key}' is a password and must be text, so it was not saved.");
                return;
            }
            if (text.Length == 0 || (onlyPlain && text.StartsWith(ProtectedPrefix, StringComparison.Ordinal))) return;
            holder[field.Key] = ProtectedPrefix + (secrets?.Protect(text)
                ?? throw new InvalidOperationException("This host cannot protect a password field, so it was not saved."));
            changed = true;
        });
        return changed;
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

    public void Save(JsonObject values)
    {
        // Only keys the plugin declared are kept, so a caller cannot store arbitrary data in the plugin's folder.
        var kept = new JsonObject();
        foreach (var field in fields)
            if (values[field.Key] is { } value) kept[field.Key] = value.DeepClone();
        // Always protect: the values come from Load (plain) or from the person, never raw from the file.
        try { ProtectPasswords(kept, onlyPlain: false); }
        catch (CryptographicException) { throw new InvalidOperationException("A password could not be protected on this PC, so the settings were not saved."); }

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
