using System.Text.Json.Nodes;

namespace MacroGrid.Plugin.Abstractions;

/// <summary>A plugin's own settings window, drawn by the host from <see cref="Fields"/> (the same
/// schema-driven form used for action settings). Registered via <see cref="IPluginHost.RegisterSettingsPage"/>.
/// A settings page can also implement <see cref="IOptionsSource"/> for its own dynamic dropdowns.</summary>
public interface IPluginSettingsPage
{
    IReadOnlyList<SettingField> Fields { get; }

    JsonObject Load();

    void Save(JsonObject values);
}
