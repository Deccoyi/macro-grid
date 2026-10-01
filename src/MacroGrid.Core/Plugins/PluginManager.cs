using System.Text.Json;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Security;
using MacroGrid.Core.Sessions;
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
    PluginLocalizer? localizer = null,
    ISecretProtector? secretProtector = null,
    PluginTrustVerifier? trustVerifier = null,
    IActiveWindowSource? windowSource = null,
    ProblemList? problems = null,
    PluginWidgetCatalog? widgetCatalog = null,
    PluginWidgetEventHub? widgetEvents = null,
    OfficialCatalog? officialCatalog = null,
    PluginInstallOriginStore? origins = null) : IHostedService, IPluginWidgetHost
{
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web);
    private readonly PluginTrustVerifier _trust = trustVerifier ?? PluginTrustVerifier.Official;
    private static readonly TimeSpan ProviderStopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>One plugin folder: its list entry, and (only while it is running) the live instance.</summary>
    private sealed class Entry(string dir, LoadedPlugin info)
    {
        public string Dir { get; } = dir;
        public LoadedPlugin Info { get; set; } = info;
        public Running? Running { get; set; }
        /// <summary>Whether the saved official safety list named this id and version when the folder was last loaded
        /// (whether or not the plugin is covered by it); lets <see cref="ApplyRevocationsAsync"/> reload only what changed.</summary>
        public bool ListedAsRevoked { get; set; }
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
        /// <summary>The handler subscribed to the instance's <see cref="IPluginTreeProvider.TreeItemsChanged"/>, if
        /// it implements that interface — kept so unloading can unsubscribe exactly it.</summary>
        public Action<string?>? TreeChangedHandler { get; set; }
        /// <summary>The plugin's answer to its widgets' requests, if it implements <see cref="IPluginWidgetHandler"/>.</summary>
        public IPluginWidgetHandler? WidgetHandler { get; set; }
    }

    private readonly Lock _stateLock = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<LoadedPlugin> _unrecognized = [];

    public IReadOnlyList<LoadedPlugin> Plugins
    {
        get
        {
            lock (_stateLock)
                return [.. _entries.Values.Select(e => e.Running?.Instance is JsPlugin js ? e.Info with { KeyboardUsesToday = js.KeyboardUsesToday } : e.Info), .. _unrecognized];
        }
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

    /// <summary>The running plugin's optional <see cref="IPluginTreeProvider"/> (its <see cref="IPlugin"/> class
    /// implements it), or null — for a plugin that does not opt in, is not running, or is not installed.</summary>
    public IPluginTreeProvider? GetTreeProvider(string pluginId) => FindEntry(pluginId)?.Running?.Instance as IPluginTreeProvider;

    /// <summary>Every <see cref="IPluginTreeProvider.TreeItemsChanged"/> of every running plugin, plus a whole-plugin
    /// entry whenever a plugin with tree items is loaded or unloaded; the editor polls it while its Plugins tool
    /// window is on screen.</summary>
    public PluginTreeChangeLog TreeChanges { get; } = new();

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

    /// <summary>The validated absolute path of a plugin's manifest icon (see <see cref="ResolveIconPath"/>),
    /// or null when it has none or it failed validation — used by the icon-serving endpoint.</summary>
    public string? GetPluginIconPath(string pluginId)
    {
        var entry = FindEntry(pluginId);
        if (entry is null) return null;
        try { return ResolveIconPath(entry.Dir, ReadManifest(Path.Combine(entry.Dir, "plugin.json"))); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) { return null; }
    }

    /// <summary>The running plugin's handler for its widgets' requests, or null when the plugin is not running or has none.</summary>
    public IPluginWidgetHandler? GetWidgetHandler(string pluginId) => FindEntry(pluginId)?.Running?.WidgetHandler;

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
        if (!IsValidPluginId(manifest.Id))
            throw new InvalidOperationException(
                $"'{manifest.Id}' is not a valid plugin id. Use 1 to 64 letters, digits, '.', '-' or '_', starting with a letter or digit and not ending with a dot.");
        if (CheckInstallTrust(sourceDir, manifest) is { } refusal)
            throw new InvalidOperationException(refusal);

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
            logger.LogInformation(SecurityEvents.PluginInstalled, "Security: plugin {Id} {Version} ({Kind}) installed", manifest.Id, manifest.Version, manifest.Kind);
            return new PluginInstallResult(manifest.Id, manifest.Name, info);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Null when the folder may be installed: a JavaScript plugin, or a C# plugin that passes the trust check
    /// (a copy of an official plugin still works). Otherwise the reason it is refused.</summary>
    public string? CheckInstallTrust(string sourceDir, PluginManifest? manifest = null)
    {
        manifest ??= ReadManifest(Path.Combine(sourceDir, "plugin.json"));
        if (manifest.Kind != PluginKind.Csharp) return null;
        var result = _trust.Verify(sourceDir, manifest);
        if (result.Allowed) return null;
        logger.LogWarning(SecurityEvents.PluginNotAllowed, "Security: C# plugin {Id} was refused at install", SecurityEvents.ForLog(manifest.Id));
        return "Only official C# plugins can be installed.";
    }

    /// <summary>Reads a folder's plugin.json without installing anything — for the editor to show what it's
    /// about to install (in particular its <see cref="PluginManifest.Kind"/>) before the person confirms.</summary>
    public static PluginManifest PeekManifest(string sourceDir) => ReadManifest(Path.Combine(sourceDir, "plugin.json"));

    /// <summary>Compares the saved safety list with what is loaded and reloads only the plugins whose answer changed: a plugin newly
    /// named is switched off, one that was taken off the list starts again. Everything else keeps running as the same instance.
    /// Reads only the saved copy; the caller refreshes it first. Returns the ids that were reloaded.</summary>
    public async Task<IReadOnlyList<string>> ApplyRevocationsAsync()
    {
        var list = officialCatalog?.Revoked;
        var changed = new List<string>();
        await _gate.WaitAsync();
        try
        {
            List<Entry> entries;
            lock (_stateLock) entries = [.. _entries.Values];
            foreach (var entry in entries)
            {
                var listed = list?.Find(entry.Info.Id, entry.Info.Version) is not null;
                if (listed == entry.ListedAsRevoked) continue;
                await UnloadCoreAsync(entry);
                lock (_stateLock) _entries.Remove(entry.Info.Id);
                await LoadFolderCoreAsync(entry.Dir);
                changed.Add(entry.Info.Id);
            }
        }
        finally { _gate.Release(); }
        return changed;
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

    /// <summary>Switches one of a JavaScript plugin's approved permissions off or on and restarts the plugin, which then runs without it
    /// (the calls that need it answer "not allowed"). Null when there is no such plugin or it did not declare that permission.</summary>
    public async Task<LoadedPlugin?> SetPermissionAsync(string pluginId, string permission, bool enabled)
    {
        await _gate.WaitAsync();
        try
        {
            var entry = FindEntry(pluginId);
            var declared = entry?.Info.Permissions?.FirstOrDefault(p => p.Equals(permission, StringComparison.OrdinalIgnoreCase));
            if (entry is null || declared is null) return null;

            permissionStore.SetSwitchedOff(pluginId, declared, off: !enabled);
            logger.LogInformation(SecurityEvents.PluginPermissionsGranted, "Security: plugin {Id} permission {Permission} was switched {State}",
                SecurityEvents.ForLog(pluginId), SecurityEvents.ForLog(declared), enabled ? "on" : "off");
            await UnloadCoreAsync(entry);
            lock (_stateLock) _entries.Remove(pluginId);
            return await LoadFolderCoreAsync(entry.Dir);
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
            logger.LogInformation(SecurityEvents.PluginPermissionsGranted, "Security: plugin {Id} was granted: {Permissions}", SecurityEvents.ForLog(pluginId), SecurityEvents.ForLog(string.Join(", ", pending)));
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
            entry.Info = entry.Info with { Status = PluginLoadStatus.Error, Detail = reason, HasSettings = false, HasTreeItems = false };
            logger.LogWarning("Plugin {Id} was switched off: {Reason}", pluginId, reason);
            widgetCatalog?.Set(pluginId, entry.Info.Name, PluginWidgetAvailability.Disabled, false, [], []);
            problems?.Report(pluginId, entry.Info.Name, ProblemSeverity.Error, ProblemCodes.SwitchedOff, reason);
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
            widgetCatalog?.Remove(pluginId);
            widgetEvents?.Forget(pluginId);
            permissionStore.Revoke(pluginId);
            logger.LogInformation(SecurityEvents.PluginUninstalled, "Security: plugin {Id} uninstalled", SecurityEvents.ForLog(pluginId));

            try
            {
                Directory.Delete(entry.Dir, recursive: true);
                return new PluginUninstallResult(true, false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Plugin folder of {Id} is still in use; it will be removed at the next start", SecurityEvents.ForLog(pluginId));
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

    /// <summary>Names the host uses for its own variables and actions, so no plugin can claim them.</summary>
    private static readonly string[] ReservedIds = ["user", "system", "core", "self"];

    private static readonly string[] ReservedDeviceNames =
        ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
         "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];

    /// <summary>The id becomes a folder name, so it is limited to characters that mean the same on every file system: no trailing dot
    /// (Windows drops it, so two ids could share a folder), no reserved device name, no spaces or other alphabets. Applied at install only;
    /// plugins that are already installed keep loading.</summary>
    internal static bool IsValidPluginId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64) return false;
        if (!char.IsAsciiLetterOrDigit(id[0]) || id[^1] == '.') return false;
        foreach (var c in id)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_')) return false;
        if (ReservedIds.Contains(id, StringComparer.OrdinalIgnoreCase)) return false;
        var stem = id.Split('.')[0];
        return !ReservedDeviceNames.Contains(stem, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The size limit is 100 KB: generous for a small square logo (an SVG is typically a few KB; a
    /// crisp 256x256 PNG rarely exceeds this), small enough that a plugin folder never balloons from it. Pixel
    /// dimensions are not decoded and checked — that would add an image-parsing dependency for a cosmetic
    /// field — so a non-square or oversized PNG under the byte limit is a plugin-author mistake caught by
    /// review, not a load-time failure. A missing, unsafe, wrong-extension or too-large icon is treated the
    /// same as no icon at all: the manifest field is cosmetic, so it never fails the plugin's own load.</summary>
    private const long MaxIconBytes = 100 * 1024;
    private static readonly string[] AllowedIconExtensions = [".svg", ".png"];

    private static string? ResolveIconPath(string dir, PluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Icon)) return null;
        if (Path.IsPathRooted(manifest.Icon) || manifest.Icon.Contains("..")) return null;

        var rootFull = Path.GetFullPath(dir);
        var iconFull = Path.GetFullPath(Path.Combine(dir, manifest.Icon));
        if (!iconFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
        if (!AllowedIconExtensions.Contains(Path.GetExtension(iconFull), StringComparer.OrdinalIgnoreCase)) return null;

        var file = new FileInfo(iconFull);
        return file.Exists && file.Length <= MaxIconBytes ? iconFull : null;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }
}
