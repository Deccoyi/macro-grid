using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using MacroGrid.Core.Model;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Widgets;

/// <summary>
/// What a placed <c>plugin-widget</c> keeps in its props: <c>plugin</c> and <c>widget</c> (which plugin's widget it is) and <c>settings</c> (what the person
/// chose). The server adds <c>runtime</c> (script, assets, limits) to the layout it sends to a device; it is never saved, so it is stripped from anything
/// that comes in (a saved profile, an import).
/// </summary>
public static partial class PluginWidgetProps
{
    public const string RuntimeKey = "runtime";
    private const int MaxVariableNameLength = 120;

    /// <summary>The plugin and widget ids a placed widget points at, or false when the props do not name them.</summary>
    public static bool TryRead(Widget widget, out string pluginId, out string widgetId)
    {
        pluginId = widgetId = "";
        if (widget.Type != WidgetTypes.PluginWidget || widget.Props is not { } props) return false;
        if (props["plugin"] is not JsonValue p || !p.TryGetValue<string>(out var plugin) || string.IsNullOrWhiteSpace(plugin)) return false;
        if (props["widget"] is not JsonValue w || !w.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id)) return false;
        pluginId = plugin;
        widgetId = id;
        return true;
    }

    /// <summary>The placed widget's settings (a copy); empty when it has none.</summary>
    public static JsonObject Settings(Widget widget) =>
        widget.Props?["settings"] is JsonObject settings ? (JsonObject)settings.DeepClone() : [];

    /// <summary>Whether a string can be a variable name a widget is allowed to ask for (the same shape plugins publish under).</summary>
    public static bool IsVariableName(string name) => name.Length is > 0 and <= MaxVariableNameLength && VariableNamePattern().IsMatch(name);

    /// <summary>The variables the person bound in this placed widget's settings (its <c>Variable</c> fields), the only outside variables the widget may read.</summary>
    public static IReadOnlySet<string> BoundVariables(PluginWidgetInfo info, JsonObject settings)
    {
        var bound = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in info.Widget.Manifest.Settings ?? [])
        {
            if (field.Kind != SettingFieldKind.Variable) continue;
            if (settings[field.Key] is JsonValue v && v.TryGetValue<string>(out var name) && IsVariableName(name)) bound.Add(name);
        }
        return bound;
    }

    /// <summary>Removes the server-made <c>runtime</c> object from every plugin widget of a profile that came from outside.</summary>
    public static void Strip(Profile profile)
    {
        foreach (var widget in profile.Pages.SelectMany(p => p.Widgets))
            if (widget.Type == WidgetTypes.PluginWidget) widget.Props?.Remove(RuntimeKey);
    }

    /// <summary>
    /// Clears, in a profile that came from someone else, every variable a plugin widget is bound to outside its own plugin (the person did not choose it,
    /// and the widget could pass it on to its plugin). Returns how many bindings were cleared; the person binds them again. For a plugin that is loaded the
    /// widget's <c>Variable</c> fields decide; for one that is not, a setting that looks like another owner's dotted variable name is cleared.
    /// </summary>
    public static int ClearOutsideBindings(Profile profile, PluginWidgetCatalog catalog)
    {
        var cleared = 0;
        foreach (var widget in profile.Pages.SelectMany(p => p.Widgets))
        {
            if (!TryRead(widget, out var pluginId, out var widgetId) || widget.Props?["settings"] is not JsonObject settings) continue;
            var info = catalog.Resolve(pluginId, widgetId, out _);
            var keys = info is not null
                ? (info.Widget.Manifest.Settings ?? []).Where(f => f.Kind == SettingFieldKind.Variable).Select(f => f.Key).ToList()
                : settings.Where(kv => kv.Value is JsonValue v && v.TryGetValue<string>(out var text) && text.Contains('.') && IsVariableName(text)).Select(kv => kv.Key).ToList();
            foreach (var key in keys)
            {
                if (settings[key] is not JsonValue v || !v.TryGetValue<string>(out var name) || name.StartsWith(pluginId + ".", StringComparison.Ordinal)) continue;
                settings.Remove(key);
                cleared++;
            }
        }
        return cleared;
    }

    [GeneratedRegex(@"^[A-Za-z0-9._\-]+$")]
    private static partial Regex VariableNamePattern();
}
