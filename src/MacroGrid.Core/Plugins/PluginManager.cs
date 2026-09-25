using System.Text.Json;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins;

public sealed record PluginInstallResult(string Id, string Name, LoadedPlugin Plugin);

/// <summary>What removing a plugin's folder achieved. <see cref="Pending"/> means the folder could not be
/// deleted right now (a file is still in use) and was marked for removal at the next start instead.</summary>
public sealed record PluginUninstallResult(bool Removed, bool Pending);

/// <summary>
/// Owns every plugin's lifetime: scans <c>plugins/&lt;Name&gt;/</c> for <c>plugin.json</c> manifests, loads each
/// <c>kind: "csharp"</c> plugin into its own <see cref="PluginLoadContext"/>, and can install, unload, reload
/// and uninstall a plugin while the server keeps running. A plugin's registrations (actions, variable
/// providers, settings page, icon packs, status items) are applied to the live registries on load and taken
/// back out on unload. <c>kind: "js"</c> manifests are recognized (so they show up in the list) but not
/// executed yet.
/// </summary>
public sealed partial class PluginManager(
    string pluginsRoot,
    string serverVersion,
    PluginStatusRegistry statusRegistry,
    ActionDispatcher dispatcher,
    VariableCatalog catalog,
    VariableProviderHost providerHost,
    VariableStore variables,
    PluginPermissionStore permissionStore,
    IInputService? input,
    ILogger<PluginManager> logger,
    PluginLocalizer? localizer = null) : IHostedService
{
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan ProviderStopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>One plugin folder: its list entry, and (only while it is running) the live instance.</summary>
    private sealed class Entry(string dir, LoadedPlugin info)
    {
        public string Dir { get; } = dir;
        public LoadedPlugin Info { get; set; } = info;
        public Running? Running { get; set; }
    }

    private sealed class Running(
        PluginLoadContext? context,
        IPlugin instance,
        PluginHostCollector host,
        TrackingVariableStore variableStore)
    {
        /// <summary>Null for a JS plugin (it has no assembly of its own).</summary>
        public PluginLoadContext? Context { get; } = context;
        public IPlugin Instance { get; } = instance;
        public PluginHostCollector Host { get; } = host;
        public TrackingVariableStore VariableStore { get; } = variableStore;
    }

    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LoadedPlugin> _unrecognized = [];

    public IReadOnlyList<LoadedPlugin> Plugins
    {
        get { lock (_stateLock) return [.. _entries.Values.Select(e => e.Info), .. _unrecognized]; }
    }

    public IReadOnlyList<IIconPackSource> IconPacks
    {
        get { lock (_stateLock) return [.. _entries.Values.Where(e => e.Running is not null).SelectMany(e => e.Running!.Host.IconPacks)]; }
    }

    /// <summary>Every running plugin's icon packs together with the id of the plugin that owns them (picks the translation table).</summary>
    public IReadOnlyList<(string PluginId, IIconPackSource Pack)> IconPacksWithOwner
    {
        get { lock (_stateLock) return [.. _entries.Values.Where(e => e.Running is not null).SelectMany(e => e.Running!.Host.IconPacks.Select(p => (e.Info.Id, p)))]; }
    }

    public IPluginSettingsPage? GetSettingsPage(string pluginId) => FindEntry(pluginId)?.Running?.Host.SettingsPage;

    public string? GetActionPluginId(string actionType)
    {
        lock (_stateLock)
            return _entries.Values.FirstOrDefault(e => e.Running?.Host.Actions.Any(a => a.Type.Equals(actionType, StringComparison.OrdinalIgnoreCase)) == true)?.Info.Id;
    }

    /// <summary>The plugins that own the given action types (built-in action types belong to no plugin and are skipped),
    /// with the types of each that were asked about. For the manifest of an exported profile.</summary>
    public IReadOnlyList<PackagePluginRef> DescribeRequiredPlugins(IEnumerable<string> actionTypes)
    {
        var byPlugin = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in actionTypes)
            if (GetActionPluginId(type) is { } id)
                (byPlugin.TryGetValue(id, out var list) ? list : byPlugin[id] = []).Add(type);

        var installed = Plugins;
        return [.. byPlugin.Select(kv =>
        {
            var info = installed.FirstOrDefault(p => p.Id.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
            return new PackagePluginRef(kv.Key, info?.Name ?? kv.Key, info?.Version ?? "?", kv.Value);
        })];
    }

    /// <summary>The plugins a package needs that are not installed and loaded here.</summary>
    public IReadOnlyList<PackagePluginRef> MissingPlugins(ProfilePackageManifest? manifest)
    {
        if (manifest is null) return [];
        var installed = Plugins;
        return [.. manifest.RequiredPlugins.Where(r =>
            !installed.Any(p => p.Id.Equals(r.Id, StringComparison.OrdinalIgnoreCase) && p.Status == PluginLoadStatus.Loaded))];
    }

    /// <summary>The install folder of a plugin by id (loaded or not), or null if that id is not installed.</summary>
    public string? GetPluginDir(string pluginId) => FindEntry(pluginId)?.Dir;

    private Entry? FindEntry(string pluginId)
    {
        lock (_stateLock)
            return _entries.GetValueOrDefault(pluginId);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try { await LoadAllCoreAsync(); }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var entry in _entries.Values.Where(e => e.Running is not null).ToList())
                await UnloadCoreAsync(entry);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Copies a plugin folder into <c>plugins/&lt;id&gt;/</c> and loads it right away. If a plugin with that
    /// id is already installed it is unloaded first and its files are overwritten (its own settings files stay).</summary>
    public async Task<PluginInstallResult> InstallFromFolderAsync(string sourceDir)
    {
        var manifest = ReadManifest(Path.Combine(sourceDir, "plugin.json"));
        if (!IsSafeSegment(manifest.Id))
            throw new InvalidOperationException($"'{manifest.Id}' is not a valid plugin id.");

        await _gate.WaitAsync();
        try
        {
            var existing = FindEntry(manifest.Id);
            if (existing is not null)
            {
                await UnloadCoreAsync(existing);
                lock (_stateLock) _entries.Remove(manifest.Id);
            }

            Directory.CreateDirectory(pluginsRoot);
            var destDir = Path.Combine(pluginsRoot, manifest.Id);
            var marker = Path.Combine(destDir, ".uninstall");
            if (File.Exists(marker)) File.Delete(marker);
            CopyDirectory(sourceDir, destDir);

            var info = await LoadFolderCoreAsync(destDir);
            return new PluginInstallResult(manifest.Id, manifest.Name, info);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Unloads a plugin and loads it again from its folder (picks up a replaced DLL or changed manifest).</summary>
    public async Task<LoadedPlugin?> ReloadAsync(string pluginId)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = FindEntry(pluginId);
            if (entry is null) return null;

            await UnloadCoreAsync(entry);
            lock (_stateLock) _entries.Remove(pluginId);
            var info = await LoadFolderCoreAsync(entry.Dir);
            return info;
        }
        finally { _gate.Release(); }
    }

    /// <summary>The user approved the permissions a JS plugin declares: remember that and start it.</summary>
    public async Task<LoadedPlugin?> ApproveAsync(string pluginId)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = FindEntry(pluginId);
            if (entry is null || entry.Info.Status != PluginLoadStatus.NeedsApproval || entry.Info.PendingPermissions is not { } pending)
                return null;

            permissionStore.Grant(pluginId, pending);
            lock (_stateLock) _entries.Remove(pluginId);
            return await LoadFolderCoreAsync(entry.Dir);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Switches a running plugin off after it kept failing (called by the plugin itself); it stays in the
    /// list with the reason, and Reload starts it again.</summary>
    private async Task DisableAsync(string pluginId, string reason)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = FindEntry(pluginId);
            if (entry?.Running is null) return;

            await UnloadCoreAsync(entry);
            entry.Info = entry.Info with { Status = PluginLoadStatus.Error, Detail = reason, HasSettings = false };
            logger.LogWarning("Plugin {Id} was switched off: {Reason}", pluginId, reason);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Unloads a plugin and deletes its folder. If a file is still in use the folder is marked for
    /// removal at the next start instead (<see cref="PluginUninstallResult.Pending"/>).</summary>
    public async Task<PluginUninstallResult?> UninstallAsync(string pluginId)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = FindEntry(pluginId);
            if (entry is null) return null;

            await UnloadCoreAsync(entry);
            lock (_stateLock) _entries.Remove(pluginId);
            permissionStore.Revoke(pluginId);

            try
            {
                Directory.Delete(entry.Dir, recursive: true);
                return new PluginUninstallResult(true, false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Plugin folder of {Id} is still in use; it will be removed at the next start", pluginId);
                File.WriteAllText(Path.Combine(entry.Dir, ".uninstall"), "");
                return new PluginUninstallResult(true, true);
            }
        }
        finally { _gate.Release(); }
    }

    // ---- helpers ----

    private static PluginManifest ReadManifest(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"plugin.json not found: {path}");
        return JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(path), ManifestJson)
            ?? throw new JsonException("plugin.json is empty");
    }

    private static bool IsSafeSegment(string id) =>
        !string.IsNullOrWhiteSpace(id) && !id.Contains("..") && id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
}
