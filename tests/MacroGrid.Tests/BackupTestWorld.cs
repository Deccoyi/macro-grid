using MacroGrid.Core.Actions;
using MacroGrid.Core.Backup;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Languages;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>A data folder with every store a backup reads, over an empty plugin folder.</summary>
internal sealed class BackupTestWorld : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));
    public ProfileStore Profiles { get; }
    public ProfileTreeStore Tree { get; }
    public PreferencesStore Preferences { get; }
    public UserVariableService Variables { get; }
    public DeviceStore Devices { get; }
    public LanguagePackStore Languages { get; }
    public PluginManager Plugins { get; }
    public BackupCollector Collector { get; }
    public BackupService Service { get; }

    public BackupTestWorld()
    {
        Directory.CreateDirectory(Root);
        var store = new VariableStore();
        Profiles = new ProfileStore(Root);
        Tree = new ProfileTreeStore(Root);
        Preferences = new PreferencesStore(Root);
        Variables = new UserVariableService(Root, store);
        Devices = new DeviceStore(Root);
        Languages = new LanguagePackStore(Root);
        var pluginsDir = Path.Combine(Root, "plugins");
        Directory.CreateDirectory(pluginsDir);
        Plugins = new PluginManager(pluginsDir, "1.0.0", new PluginStatusRegistry(), new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance),
            new VariableCatalog([]), new VariableProviderHost([], store, NullLogger<VariableProviderHost>.Instance), store,
            new PluginPermissionStore(Root), null, NullLogger<PluginManager>.Instance, trustVerifier: TestPluginSigning.Lenient);
        Collector = new BackupCollector(Profiles, Tree, Preferences, Variables, Devices, Plugins, Languages);
        Service = new BackupService(Collector, Root, "1.0.0");
    }

    public string RestorePointsFolder => Path.Combine(Root, BackupService.FolderName);

    public Profile AddProfile(string name)
    {
        var profile = new Profile { Name = name, Pages = [new Page { Name = "Page 1", Widgets = [new Widget { Id = "w1", Text = name }] }] };
        WidgetNames.Ensure(profile);
        Profiles.Save(profile);
        return profile;
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }
}
