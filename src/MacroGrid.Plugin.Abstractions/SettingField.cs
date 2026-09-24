using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>Serializes as its exact member name regardless of the caller's JsonSerializerOptions (same
/// pattern as PluginManifest.PluginKind) — /api/actions returns this via ASP.NET's default camelCase
/// property naming, /api/plugins/{id}/settings/schema via ProtocolJson.Options; both must agree.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SettingFieldKind>))]
public enum SettingFieldKind
{
    Text,
    Password,
    Number,
    Slider,
    Bool,
    Select,
    Segmented,
}

/// <summary>One labeled option in a <see cref="SettingFieldKind.Select"/> or <see cref="SettingFieldKind.Segmented"/> field.
/// <paramref name="Group"/> is used for indentation like "Grup › Öğe" (e.g. a scene item nested in a group).</summary>
public sealed record SettingOption(string Value, string Label, string? Group = null, string? Icon = null);

/// <summary>The result of an <see cref="IOptionsSource"/> query. <paramref name="Error"/> carries a
/// user-facing message (e.g. "OBS'e bağlı değil") when options could not be produced; in that case
/// <paramref name="Options"/> is empty, not null.</summary>
public sealed record OptionsResult(IReadOnlyList<SettingOption> Options, string? Error = null);

/// <summary>Declares one field of a schema-driven form. The host renders the form (action settings or
/// plugin settings) from a list of these; the plugin never draws UI itself.</summary>
public sealed record SettingField(string Key, string Label, SettingFieldKind Kind)
{
    public string? Description { get; init; }
    public string? Placeholder { get; init; }
    public JsonNode? Default { get; init; }

    public double? Min { get; init; }
    public double? Max { get; init; }
    public double? Step { get; init; }

    /// <summary>Static options for Select/Segmented. Use <see cref="OptionsSource"/> instead for dynamic lists.</summary>
    public SettingOption[]? Options { get; init; }

    /// <summary>Id passed to <see cref="IOptionsSource.GetOptionsAsync"/> to fetch options dynamically.</summary>
    public string? OptionsSource { get; init; }

    /// <summary>Keys whose current form values are passed to the options query, and which trigger a
    /// refetch when they change (e.g. a scene-item field depends on the selected scene).</summary>
    public string[]? DependsOn { get; init; }

    /// <summary>Shows the {var} insert button on a text field.</summary>
    public bool AllowVariables { get; init; }

    /// <summary>Only show this field when another field's value equals the given one, e.g. "mode=pause".</summary>
    public string? VisibleWhen { get; init; }
}
