namespace MacroGrid.Core.Plugins;

/// <summary>
/// The plugin folder the person picked in the native folder dialog and has not installed yet. The install confirmation uses this
/// folder and nothing the client sends, so a request cannot name any other path on the PC to be read and copied.
/// </summary>
public sealed class PluginInstallSelection
{
    private readonly Lock _lock = new();
    private string? _folder;

    /// <summary>Remembers the folder the person just picked (replacing an earlier one).</summary>
    public void Set(string folder)
    {
        lock (_lock) _folder = Path.GetFullPath(folder);
    }

    /// <summary>The remembered folder if <paramref name="claimed"/> (what the editor says it is installing) names the same folder,
    /// otherwise null. The choice is used up: a second confirmation needs a new pick.</summary>
    public string? Take(string? claimed)
    {
        lock (_lock)
        {
            var folder = _folder;
            _folder = null;
            if (folder is null || string.IsNullOrWhiteSpace(claimed)) return null;
            return string.Equals(Path.GetFullPath(claimed), folder, StringComparison.OrdinalIgnoreCase) ? folder : null;
        }
    }
}
