using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>Optional side interface of an <see cref="IPluginSettingsPage"/> (the same pattern as
/// <see cref="IOptionsSource"/>): runs a <see cref="SettingFieldKind.Button"/> field's <c>command</c> — a
/// generic hook any plugin can use for whatever a form needs a button for (a sound preview, an OBS
/// connection test, clearing a cache, ...); the SDK does not fix what a command does or how many a page
/// has. <paramref name="command"/> is the field's own <c>Command</c> string, so one settings page can expose
/// several distinct buttons. <paramref name="values"/> holds the current values of the form the button sits
/// in, or of its row when the button is inside a <see cref="SettingFieldKind.List"/>. The returned text is
/// shown in the editor as a short info/error message; null shows nothing.</summary>
public interface ISettingsCommandHandler
{
    Task<string?> RunCommandAsync(string command, JsonObject values, CancellationToken cancellationToken);
}
