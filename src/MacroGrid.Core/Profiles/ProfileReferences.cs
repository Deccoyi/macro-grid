using System.Text.Json;
using MacroGrid.Core.Model;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Profiles;

/// <summary>A file named by an action field that does not exist on this PC.</summary>
public sealed record MissingFile(string Page, string Widget, string Path);

/// <summary>Finds what a profile points at outside itself, so an import can say what will not work here.</summary>
public static class ProfileReferences
{
    /// <summary>Files named in <see cref="SettingFieldKind.File"/> action fields that do not exist (empty values are not references).</summary>
    public static IReadOnlyList<MissingFile> MissingFiles(IEnumerable<Profile> profiles, IEnumerable<IActionHandler> handlers)
    {
        var fileKeys = handlers.Where(h => h is IActionDescriptor)
            .ToDictionary(h => h.Type, h => ((IActionDescriptor)h).Fields.Where(f => f.Kind == SettingFieldKind.File).Select(f => f.Key).ToArray(), StringComparer.OrdinalIgnoreCase);
        var found = new List<MissingFile>();
        foreach (var profile in profiles)
            foreach (var page in profile.Pages)
                foreach (var widget in page.Widgets)
                    foreach (var binding in widget.Actions.Values.SelectMany(b => b))
                    {
                        if (!fileKeys.TryGetValue(binding.Type, out var keys)) continue;
                        foreach (var key in keys)
                        {
                            if (binding.Settings[key] is not { } node || node.GetValueKind() != JsonValueKind.String) continue;
                            var path = node.GetValue<string>();
                            if (path.Length == 0 || File.Exists(path)) continue;
                            if (!found.Any(f => f.Path == path && f.Widget == (widget.Name ?? widget.Id))) found.Add(new MissingFile(page.Name, widget.Name ?? widget.Id, path));
                        }
                    }
        return found;
    }
}
