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

    /// <summary>A path text box plus a Browse button; the host shows a native file picker filtered by <see cref="SettingField.FileFilter"/>.</summary>
    File,

    /// <summary>Repeated rows, each shaped by <see cref="SettingField.ItemFields"/>. The value is a <c>JsonArray</c> of <c>JsonObject</c>.</summary>
    List,

    /// <summary>A button; clicking it sends <see cref="SettingField.Command"/> and the current form/row values to
    /// the plugin's <see cref="ISettingsCommandHandler"/> — a generic "run this and show me what happened" hook,
    /// e.g. a preview, a connection test or a one-off cleanup action. Not a value field — never appears in the
    /// saved settings.</summary>
    Button,

    /// <summary>Read-only text, styled as a warning. Not a value field — never appears in the saved settings.</summary>
    Notice,
}

/// <summary>One labeled option in a <see cref="SettingFieldKind.Select"/> or <see cref="SettingFieldKind.Segmented"/> field.
/// <paramref name="Group"/> is used for indentation like "Group › Item" (e.g. a scene item nested in a group).</summary>
public sealed record SettingOption(string Value, string Label, string? Group = null, string? Icon = null);

/// <summary>The result of an <see cref="IOptionsSource"/> query. <paramref name="Error"/> carries a
/// user-facing message (e.g. "Not connected to the app") when options could not be produced; in that case
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

    /// <summary>Only show this field when another field's value equals the given one, e.g. "mode=pause".
    /// Inside a <see cref="SettingFieldKind.List"/> row, evaluated against that row's own values.</summary>
    public string? VisibleWhen { get; init; }

    /// <summary>Required for <see cref="SettingFieldKind.File"/>: a WinForms file filter, passed to the host's
    /// native file picker as-is — the plugin decides what it accepts, e.g. <c>"Audio files (*.wav;*.mp3)|*.wav;*.mp3"</c>
    /// or <c>"Images (*.png;*.jpg)|*.png;*.jpg"</c>. The SDK has no opinion on file types.</summary>
    public string? FileFilter { get; init; }

    /// <summary>Required for <see cref="SettingFieldKind.List"/>: the schema of one row. Row keys outside this
    /// schema (a plugin-assigned id, a computed flag, ...) are preserved by the editor across a save.</summary>
    public SettingField[]? ItemFields { get; init; }

    /// <summary>Required for <see cref="SettingFieldKind.Button"/>: the command id sent to
    /// <see cref="ISettingsCommandHandler.RunCommandAsync"/> when the button is clicked.</summary>
    public string? Command { get; init; }
}
