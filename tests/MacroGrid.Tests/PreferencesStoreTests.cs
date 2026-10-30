using MacroGrid.Core.Preferences;

namespace MacroGrid.Tests;

public sealed class PreferencesStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-prefs-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void A_closed_notice_survives_a_restart()
    {
        var store = new PreferencesStore(_dir);
        var preferences = store.Get();
        preferences.DismissedNotices["web.warning"] = true;
        store.Save(preferences);

        var reloaded = new PreferencesStore(_dir).Get();

        Assert.True(reloaded.DismissedNotices["web.warning"]);
    }

    [Fact]
    public void A_preferences_file_from_an_older_version_has_no_closed_notices()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "preferences.json"), """{ "theme": "dark", "collapsedInspectorSections": { "css": false } }""");

        var loaded = new PreferencesStore(_dir).Get();

        Assert.Empty(loaded.DismissedNotices);
        Assert.False(loaded.CollapsedInspectorSections["css"]);
    }
}
