namespace MacroGrid.Core.Model;

public sealed class Profile
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "Profile";
    public List<Page> Pages { get; set; } = [];

    /// <summary>Which "Preview" device preset (or preview profile id) the editor should switch to when
    /// this profile is opened — null/empty means "free". Purely an editor convenience; the
    /// server/client never read it. If the referenced preset no longer exists (a custom preview profile
    /// was deleted in Preferences) the editor falls back to free on its own — this field is left as-is.</summary>
    public string? PreviewDeviceId { get; set; }

    /// <summary>Foreground-window rules that auto-switch an opted-in device to this profile — see
    /// docs/design/auto-profile-switch.md. Empty means this profile never triggers an automatic switch.</summary>
    public List<AppMatch> AppMatches { get; set; } = [];

    public Page? FindPage(string pageId) => Pages.FirstOrDefault(p => p.Id == pageId);

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}
