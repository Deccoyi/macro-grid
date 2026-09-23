using System.Text.Json.Serialization;

namespace MacroStation.Plugin.Abstractions;

[JsonConverter(typeof(JsonStringEnumConverter<StatusLevel>))]
public enum StatusLevel
{
    Idle,
    Ok,
    Busy,
    Warning,
    Error,
}

/// <summary>A single entry a plugin owns in the editor's window-wide status bar. Created once via
/// <see cref="IPluginHost.CreateStatusItem"/> and updated as often as the plugin's state changes.</summary>
public interface IPluginStatusItem
{
    void Update(string text, StatusLevel level, string? icon = null, string? tooltip = null);
}
