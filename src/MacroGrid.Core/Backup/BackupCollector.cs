using System.Text.Json.Nodes;
using MacroGrid.Core.Automation;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Languages;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;

namespace MacroGrid.Core.Backup;

/// <summary>Reads what a backup holds from the stores (not from the files on disk, so a half-written or broken file never travels).</summary>
public class BackupCollector(
    ProfileStore profiles,
    ProfileTreeStore tree,
    PreferencesStore preferences,
    UserVariableService variables,
    AutomationStore automation,
    DeviceStore devices,
    PluginManager plugins,
    LanguagePackStore languages)
{
    public virtual BackupContent Collect()
    {
        var allProfiles = profiles.All;
        var content = new BackupContent
        {
            Profiles = [.. allProfiles],
            ProfileTree = tree.GetNormalized(allProfiles),
            Preferences = preferences.Get(),
            Variables = [.. variables.List()],
            AutomationRules = [.. automation.List()],
            // Built field by field: the token and the pairing times are never read into a backup.
            Devices = [.. devices.All.Select(d => new BackupDevice(d.Id, d.Name, d.AssignedProfileId, d.FollowActiveWindow, d.AutoSwitchLocked))],
        };

        // Only a running plugin with a settings page can say which of its fields are passwords; a raw settings file cannot be told apart.
        foreach (var plugin in plugins.Plugins)
        {
            if (plugins.GetSettingsPage(plugin.Id) is not { } page) continue;
            if (TryReadSettings(plugin.Id, () => PluginSettingsSecrets.Strip(page.Fields, page.Load()), content.Warnings) is { } settings)
                content.PluginSettings[plugin.Id] = settings;
        }

        foreach (var info in languages.List())
            if (languages.Read(info.Tag) is { } text) content.LanguagePacks[info.Tag] = text;

        return content;
    }

    /// <summary>A plugin's own code runs inside the read, so whatever it throws (a bad file, a broken script) leaves only that plugin's
    /// settings out of the backup, with a warning; it never stops the backup or, at start-up, the restore point of an update.</summary>
    internal static JsonObject? TryReadSettings(string pluginId, Func<JsonObject> read, List<string> warnings)
    {
        try { return read(); }
        catch (Exception ex)
        {
            warnings.Add($"The settings of plugin {pluginId} were left out: {ex.GetType().Name}.");
            return null;
        }
    }

    /// <summary>The plugins the actions in these profiles need (for the manifest, and for the warning on another PC).</summary>
    public IReadOnlyList<PackagePluginRef> RequiredPlugins(IEnumerable<Profile> profilesToCheck) =>
        plugins.DescribeRequiredPlugins(profilesToCheck.SelectMany(ProfilePackage.ActionTypes).Distinct(StringComparer.OrdinalIgnoreCase));
}
