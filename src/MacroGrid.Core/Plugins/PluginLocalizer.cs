using System.Text.Json;
using System.Text.RegularExpressions;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins;

/// <summary>
/// Translates the texts a plugin hands to the editor (action names and descriptions, category names, variable
/// descriptions, settings form labels) into the language the person chose in the preferences.
/// A plugin writes those texts in its own default language (manifest <c>defaultLanguage</c>, "en" when absent) and may
/// ship <c>locales/&lt;language&gt;.json</c> next to its <c>plugin.json</c>: one object that maps the default-language text to
/// its translation. A language without a file, or a text without an entry, falls back to the text as the plugin wrote it.
/// A text with a value put into it at run time is translated through a template: the key holds <c>{0}</c>, <c>{1}</c> ... where the values
/// go (<c>"Plugin · retrying in {0}s"</c>) and the translation moves them where the language wants them.
/// </summary>
public sealed class PluginLocalizer(Func<string> currentLanguage)
{
    private sealed record PluginTexts(string DefaultLanguage, Dictionary<string, Dictionary<string, string>> ByLanguage);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, PluginTexts> _plugins = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string pluginId, string pluginDir, string? defaultLanguage)
    {
        var byLanguage = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var localesDir = Path.Combine(pluginDir, "locales");
        if (Directory.Exists(localesDir))
        {
            foreach (var file in Directory.EnumerateFiles(localesDir, "*.json"))
            {
                try
                {
                    var table = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                    if (table is not null) byLanguage[Path.GetFileNameWithoutExtension(file)] = table;
                }
                catch (Exception ex) when (ex is JsonException or IOException)
                {
                    // A broken translation file must not stop the plugin; its texts stay in the default language.
                }
            }
        }
        lock (_lock) _plugins[pluginId] = new PluginTexts(NormalizeLanguage(defaultLanguage) ?? "en", byLanguage);
    }

    public void Unregister(string pluginId)
    {
        lock (_lock) _plugins.Remove(pluginId);
    }

    /// <summary>The text in the current language, or <paramref name="text"/> itself when there is no translation.</summary>
    public string? Translate(string? pluginId, string? text)
    {
        if (pluginId is null || string.IsNullOrEmpty(text)) return text;
        PluginTexts? texts;
        lock (_lock) _plugins.TryGetValue(pluginId, out texts);
        if (texts is null) return text;

        var language = NormalizeLanguage(currentLanguage()) ?? texts.DefaultLanguage;
        if (language == texts.DefaultLanguage) return text;
        return texts.ByLanguage.TryGetValue(language, out var table) ? Lookup(table, text) : text;
    }

    /// <summary>Translates a text whose plugin is not known (for example an action error that reaches the status bar): the first plugin
    /// that has a translation for it wins.</summary>
    public string? TranslateAny(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string[] ids;
        lock (_lock) ids = [.. _plugins.Keys];
        foreach (var id in ids)
        {
            var translated = Translate(id, text);
            if (!ReferenceEquals(translated, text) && translated != text) return translated;
        }
        return text;
    }

    private static string Lookup(Dictionary<string, string> table, string text)
    {
        if (table.TryGetValue(text, out var exact)) return exact;
        foreach (var (key, value) in table)
        {
            if (!key.Contains("{0}")) continue;
            var pattern = "^" + Regex.Replace(Regex.Escape(key), @"\\\{(\d+)\}", m => $"(?<v{m.Groups[1].Value}>.+?)") + "$";
            var match = Regex.Match(text, pattern, RegexOptions.Singleline);
            if (!match.Success) continue;
            return Regex.Replace(value, @"\{(\d+)\}", m => match.Groups["v" + m.Groups[1].Value] is { Success: true } g ? g.Value : m.Value);
        }
        return text;
    }

    public SettingField Localize(string? pluginId, SettingField field) => field with
    {
        Label = Translate(pluginId, field.Label)!,
        Description = Translate(pluginId, field.Description),
        Placeholder = Translate(pluginId, field.Placeholder),
        Options = field.Options?.Select(o => o with { Label = Translate(pluginId, o.Label)! }).ToArray(),
    };

    public VariableInfo Localize(string? pluginId, VariableInfo variable) => variable with
    {
        Description = Translate(pluginId, variable.Description)!,
        Category = Translate(pluginId, variable.Category)!,
    };

    /// <summary>"tr-TR" and "TR" both become "tr"; empty or missing becomes null.</summary>
    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var trimmed = language.Trim();
        var dash = trimmed.IndexOfAny(['-', '_']);
        return (dash > 0 ? trimmed[..dash] : trimmed).ToLowerInvariant();
    }
}
