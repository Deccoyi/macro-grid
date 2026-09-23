namespace MacroStation.Plugin.Abstractions;

/// <summary>A set of icons a plugin contributes to the editor's icon picker, registered via
/// <see cref="IPluginHost.RegisterIconPack"/>. Owned by the icon-pack workstream; the OBS/settings-form
/// workstream only depends on the signature agreed between the two, kept here so the host compiles.</summary>
public interface IIconPackSource
{
    string Id { get; }

    string DisplayName { get; }

    IReadOnlyList<string> IconNames { get; }

    string? GetIconSvg(string name);
}
