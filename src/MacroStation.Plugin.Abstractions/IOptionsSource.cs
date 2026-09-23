using System.Text.Json.Nodes;

namespace MacroStation.Plugin.Abstractions;

/// <summary>Implemented by an action handler or a settings page to serve dynamic dropdown options
/// (scenes, audio inputs, scene items, ...) for a <see cref="SettingField"/> whose <c>OptionsSource</c>
/// names it. <paramref name="currentValues"/> holds the current form values for the keys listed in the
/// field's <c>DependsOn</c>.</summary>
public interface IOptionsSource
{
    Task<OptionsResult> GetOptionsAsync(string sourceId, JsonObject currentValues, CancellationToken cancellationToken);
}
