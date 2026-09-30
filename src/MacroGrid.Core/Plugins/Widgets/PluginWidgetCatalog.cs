using System.Text;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Plugins.Widgets;

/// <summary>Why a plugin's widgets cannot run right now. The device draws a placeholder that says which.</summary>
public enum PluginWidgetAvailability
{
    Available,
    /// <summary>The plugin failed to load or was switched off.</summary>
    Disabled,
    NeedsApproval,
    Incompatible,
}

/// <summary>
/// One plugin widget ready to send: the manifest data, the frame cap that applies, and the references under which its script and assets are
/// held in the <see cref="AssetStore"/> (the device fetches each once and caches it, so a new plugin version is simply a new hash).
/// </summary>
public sealed record PluginWidgetInfo(
    string PluginId, string PluginName, ValidatedWidget Widget, bool Verified, string CodeRef, IReadOnlyDictionary<string, string> AssetRefs,
    PluginKind Kind = PluginKind.Js);

/// <summary>
/// The custom widgets of every plugin, as the loader validated them. The loader fills it on every load outcome and empties it on unload; the layout
/// sender and the widget router only read it. It reads each widget's script and images once (when the plugin loads) and keeps them in memory.
/// </summary>
public sealed class PluginWidgetCatalog(AssetStore assets)
{
    private sealed record PluginEntry(
        string Name, PluginWidgetAvailability Availability, bool Verified,
        Dictionary<string, PluginWidgetInfo> Widgets, Dictionary<string, string> Problems, Dictionary<string, string> Payload);

    private readonly Lock _lock = new();
    private readonly Dictionary<string, PluginEntry> _plugins = new(StringComparer.Ordinal);

    /// <summary>Raised after the widgets of a plugin changed (loaded, switched off, reloaded, removed), outside the lock.</summary>
    public event Action<string>? PluginChanged;

    /// <summary>Records what a plugin's load outcome means for its widgets. <paramref name="widgets"/> are already validated; their files are read here.</summary>
    public void Set(string pluginId, string pluginName, PluginWidgetAvailability availability, bool verified,
        IReadOnlyList<ValidatedWidget> widgets, IReadOnlyList<PluginWidgetProblem> problems, PluginKind kind = PluginKind.Js)
    {
        var infos = new Dictionary<string, PluginWidgetInfo>(StringComparer.Ordinal);
        var problemMap = problems.ToDictionary(p => p.WidgetId, p => p.Reason, StringComparer.Ordinal);
        var payload = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var widget in widgets)
        {
            try
            {
                var code = File.ReadAllText(widget.EntryPath, Encoding.UTF8);
                var codeRef = Track(payload, code);
                var assetRefs = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var (name, path) in widget.Assets)
                    assetRefs[name] = Track(payload, ToDataUri(path));
                infos[widget.Manifest.Id] = new PluginWidgetInfo(pluginId, pluginName, widget, verified, codeRef, assetRefs, kind);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                problemMap[widget.Manifest.Id] = $"The widget's files could not be read: {ex.Message}";
            }
        }

        lock (_lock)
        {
            _plugins[pluginId] = new PluginEntry(pluginName, availability, verified, infos, problemMap, payload);
        }
        PluginChanged?.Invoke(pluginId);
    }

    public void Remove(string pluginId)
    {
        bool removed;
        lock (_lock) removed = _plugins.Remove(pluginId);
        if (removed) PluginChanged?.Invoke(pluginId);
    }

    /// <summary>Finds a widget, or says why it cannot run: <c>missing</c> (no such plugin), <c>disabled</c>, <c>needsApproval</c>,
    /// <c>incompatible</c>, <c>invalid</c> (the manifest entry was refused) or <c>noWidget</c> (the plugin has no widget with this id).</summary>
    public PluginWidgetInfo? Resolve(string pluginId, string widgetId, out string? unavailable)
    {
        PluginEntry? entry;
        lock (_lock) _plugins.TryGetValue(pluginId, out entry);
        if (entry is null) { unavailable = "missing"; return null; }
        unavailable = entry.Availability switch
        {
            PluginWidgetAvailability.Disabled => "disabled",
            PluginWidgetAvailability.NeedsApproval => "needsApproval",
            PluginWidgetAvailability.Incompatible => "incompatible",
            _ => null,
        };
        if (unavailable is not null) return null;
        if (entry.Widgets.TryGetValue(widgetId, out var info))
        {
            // Every send re-touches the stored values, so ones a layout still references never fall off the store.
            foreach (var value in entry.Payload.Values) assets.Put(value);
            return info;
        }
        unavailable = entry.Problems.ContainsKey(widgetId) ? "invalid" : "noWidget";
        return null;
    }

    /// <summary>Why a widget was refused, for the Plugins window and the Error List.</summary>
    public string? ProblemOf(string pluginId, string widgetId)
    {
        lock (_lock)
            return _plugins.TryGetValue(pluginId, out var entry) && entry.Problems.TryGetValue(widgetId, out var reason) ? reason : null;
    }

    /// <summary>Every widget that can run now, for the toolbox.</summary>
    public IReadOnlyList<PluginWidgetInfo> Available()
    {
        lock (_lock)
            return [.. _plugins.Values.Where(p => p.Availability == PluginWidgetAvailability.Available).SelectMany(p => p.Widgets.Values)];
    }

    private string Track(Dictionary<string, string> payload, string data)
    {
        var reference = assets.Put(data);
        payload[reference] = data;
        return reference;
    }

    private static string ToDataUri(string path)
    {
        var mime = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };
        return $"data:{mime};base64,{Convert.ToBase64String(File.ReadAllBytes(path))}";
    }
}
