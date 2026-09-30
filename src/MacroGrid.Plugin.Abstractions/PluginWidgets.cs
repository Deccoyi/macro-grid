using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>The size a plugin widget takes on the grid when the person drops it, in grid cells.</summary>
public sealed record PluginWidgetSize(int W, int H);

/// <summary>One thing a widget may do beyond drawing and reading its plugin's data, declared by the author and approved by the person
/// when the plugin is installed. A widget can never use an option its manifest does not declare.</summary>
public sealed record PluginWidgetOption
{
    /// <summary>Whether the option starts switched on for a placed widget. The person can switch it off later, per widget or per plugin.</summary>
    public bool Default { get; init; } = true;
}

/// <summary>
/// One custom widget a plugin adds to the toolbox, from the <c>widgets</c> array of its <c>plugin.json</c>. The widget's code is one
/// bundled classic script that runs in a sandboxed worker and draws to a canvas (docs/design/plugin-widgets.md); it has no network access.
/// Additive: an older host ignores the array.
/// </summary>
public sealed record PluginWidgetManifest
{
    /// <summary>Unique within the plugin: letters, digits, '-' and '_'.</summary>
    public required string Id { get; init; }

    /// <summary>Shown in the toolbox and in Properties.</summary>
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>Groups the widget in the toolbox ("Gauges"). Optional.</summary>
    public string? Category { get; init; }

    /// <summary>Path of the widget's script inside the plugin folder (for example "widgets/gauge.js"): one bundled classic script.</summary>
    public required string Entry { get; init; }

    /// <summary>Package files the script can load by name (png, jpeg, webp images and woff2 fonts), as paths inside the plugin folder.</summary>
    public string[]? Assets { get; init; }

    /// <summary>Path of the widget's Toolbox icon inside the plugin folder: a small SVG file (at most 8 KB), no scripts and no outside links. Optional.</summary>
    public string? Icon { get; init; }

    /// <summary>The size on the grid when dropped; defaults to 2 x 2.</summary>
    public PluginWidgetSize? Size { get; init; }

    /// <summary>The most frames per second the widget draws, 1 to 60; 15 when left out. Plugins that are not verified are held to 30.</summary>
    public int? Fps { get; init; }

    /// <summary>True when the widget wants pointer input (touch and mouse) on its canvas.</summary>
    public bool Interactive { get; init; }

    /// <summary>Extra abilities the widget declares by name (<c>keepLoaded</c> or <c>storage</c>). Approved at install.</summary>
    public Dictionary<string, PluginWidgetOption>? Options { get; init; }

    /// <summary>The settings a person edits for a placed widget, drawn by the editor's schema-driven form. A <c>Variable</c> field lets
    /// the person bind one variable to the widget. <c>Password</c> and <c>File</c> fields are not allowed here.</summary>
    public SettingField[]? Settings { get; init; }
}

/// <summary>What a widget sent to its plugin with <c>macroGrid.request(...)</c>.</summary>
/// <param name="Widget">The manifest's widget id, for example "gauge".</param>
/// <param name="DeviceId">The device the widget is shown on.</param>
/// <param name="PageId">The page of the profile.</param>
/// <param name="WidgetId">The placed widget's id in the profile.</param>
/// <param name="Settings">This placed widget's settings, bindings included.</param>
/// <param name="Data">What the widget sent.</param>
public sealed record PluginWidgetMessage(string Widget, string DeviceId, string PageId, string WidgetId, JsonObject Settings, JsonNode? Data);

/// <summary>Optional, implemented by the plugin's <see cref="IPlugin"/> class: answers the requests of the plugin's widgets.
/// A plugin with widgets but no handler still works; a widget's request then gets "not_allowed".</summary>
public interface IPluginWidgetHandler
{
    /// <summary>Called for a widget's <c>macroGrid.request(...)</c>. Return the reply (null for none). Cancelled after 2 seconds.</summary>
    Task<JsonNode?> OnWidgetMessageAsync(PluginWidgetMessage message, CancellationToken cancellationToken);
}

/// <summary>Lets a plugin push events to its own widgets. Host-implemented.</summary>
public interface IPluginWidgets
{
    /// <summary>Sends an event to the plugin's own widgets. <paramref name="widgetId"/> and <paramref name="deviceId"/> narrow the target;
    /// with <paramref name="retain"/> the last event of each name is kept and given to a widget that comes on screen later.</summary>
    void Post(string widget, string name, JsonNode? data, string? widgetId = null, string? deviceId = null, bool retain = false);
}
