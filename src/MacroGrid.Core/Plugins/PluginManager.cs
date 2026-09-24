using System.Text.Json;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Plugins.Js;
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
public sealed class PluginManager(
    string pluginsRoot,
    string serverVersion,
    PluginStatusRegistry statusRegistry,
    ActionDispatcher dispatcher,
    VariableCatalog catalog,
    VariableProviderHost providerHost,
    VariableStore variables,
    PluginPermissionStore permissionStore,
    IInputService? input,
    ILogger<PluginManager> logger) : IHostedService
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

    public IPluginSettingsPage? GetSettingsPage(string pluginId)
    {
        lock (_stateLock)
            return _entries.TryGetValue(pluginId, out var e) ? e.Running?.Host.SettingsPage : null;
    }

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
    public string? GetPluginDir(string pluginId)
    {
        lock (_stateLock)
            return _entries.TryGetValue(pluginId, out var e) ? e.Dir : null;
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

    private async Task LoadAllCoreAsync()
    {
        if (!Directory.Exists(pluginsRoot)) return;

        foreach (var dir in Directory.EnumerateDirectories(pluginsRoot))
        {
            var folderName = Path.GetFileName(dir);

            // Deferred uninstall: a plugin folder that could not be deleted while its files were in use is marked
            // with this file. Finish the removal here, before anything gets a chance to load it again.
            if (File.Exists(Path.Combine(dir, ".uninstall")))
            {
                try { Directory.Delete(dir, recursive: true); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogError(ex, "Could not delete the plugin folder marked for removal: {Folder}", folderName);
                }
                continue;
            }

            if (!File.Exists(Path.Combine(dir, "plugin.json"))) continue;
            await LoadFolderCoreAsync(dir);
        }
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
            Entry? existing;
            lock (_stateLock) _entries.TryGetValue(manifest.Id, out existing);
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
            Entry? entry;
            lock (_stateLock) _entries.TryGetValue(pluginId, out entry);
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
            Entry? entry;
            lock (_stateLock) _entries.TryGetValue(pluginId, out entry);
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
            Entry? entry;
            lock (_stateLock) _entries.TryGetValue(pluginId, out entry);
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
            Entry? entry;
            lock (_stateLock) _entries.TryGetValue(pluginId, out entry);
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

    // ---- loading ----

    /// <summary>Validates and loads one plugin folder and records it. Never throws for a bad plugin: the failure
    /// becomes an Error / Incompatible entry in the list, like at startup.</summary>
    private async Task<LoadedPlugin> LoadFolderCoreAsync(string dir)
    {
        var folderName = Path.GetFileName(dir);

        PluginManifest manifest;
        try
        {
            manifest = ReadManifest(Path.Combine(dir, "plugin.json"));
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            logger.LogError(ex, "Could not read the plugin manifest: {Folder}", folderName);
            var broken = new LoadedPlugin(folderName, folderName, "?", PluginLoadStatus.Error, $"plugin.json could not be parsed: {ex.Message}");
            lock (_stateLock) _unrecognized.Add(broken);
            return broken;
        }

        lock (_stateLock)
            _unrecognized.RemoveAll(p => p.Id == folderName);

        LoadedPlugin Fail(PluginLoadStatus status, string detail, IReadOnlyList<string>? pending = null)
        {
            var info = new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, status, detail, false, pending);
            lock (_stateLock)
            {
                // A second folder with the same id must not replace the first one's entry.
                if (_entries.TryGetValue(manifest.Id, out var existing) && !string.Equals(existing.Dir, dir, StringComparison.OrdinalIgnoreCase))
                    _unrecognized.Add(info);
                else
                    _entries[manifest.Id] = new Entry(dir, info);
            }
            return info;
        }

        if (GetPluginDir(manifest.Id) is { } takenBy && !string.Equals(takenBy, dir, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Plugin id clash: {Id} ({Folder})", manifest.Id, folderName);
            return Fail(PluginLoadStatus.Error, "This id is already used by another installed plugin");
        }

        if (!SemVer.SatisfiesCaret(PluginSdk.Version, manifest.SdkVersion))
            return Fail(PluginLoadStatus.Incompatible, $"Needs SDK {manifest.SdkVersion}, the server has SDK {PluginSdk.Version}");

        if (!SemVer.SatisfiesMinimum(serverVersion, manifest.MinServerVersion))
            return Fail(PluginLoadStatus.Incompatible, $"Needs server {manifest.MinServerVersion}+, this server is {serverVersion}");

        var entryPath = Path.Combine(dir, manifest.Entry);
        if (!File.Exists(entryPath))
            return Fail(PluginLoadStatus.Error, $"Entry file not found: {manifest.Entry}");

        var variableStore = new TrackingVariableStore(variables);
        string[] declared = [.. manifest.Permissions ?? []];
        if (manifest.Kind == PluginKind.Js)
        {
            var unknown = declared.FirstOrDefault(p => !JsPermissions.IsKnown(p));
            if (unknown is not null)
                return Fail(PluginLoadStatus.Error, $"Unknown permission '{unknown}'");
            if (!permissionStore.IsGranted(manifest.Id, declared))
                return Fail(PluginLoadStatus.NeedsApproval, "Needs your approval before it can run", declared);
        }

        PluginLoadContext? context = null;
        PluginHostCollector? host = null;
        var registered = new List<IActionHandler>();
        IPlugin? instance = null;
        try
        {
            if (manifest.Kind == PluginKind.Js)
            {
                instance = new JsPlugin(manifest, entryPath, new JsPermissions(declared), variableStore, input, logger,
                    reason => _ = Task.Run(() => DisableAsync(manifest.Id, reason)));
            }
            else
            {
                context = new PluginLoadContext(manifest.Id, entryPath);
                var assembly = context.LoadPluginAssembly(entryPath);
                var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    ?? throw new InvalidOperationException($"No type implementing IPlugin was found in {manifest.Entry}");
                instance = (IPlugin)(Activator.CreateInstance(pluginType) ?? throw new InvalidOperationException("The plugin instance could not be created"));
            }

            host = new PluginHostCollector(serverVersion, dir, manifest.Id, statusRegistry, logger);
            instance.Initialize(host);

            // All-or-nothing: a clash on any action type fails the whole plugin instead of half-registering it.
            foreach (var action in host.Actions)
            {
                if (!dispatcher.Register(action))
                    throw new InvalidOperationException($"Action type '{action.Type}' is already registered");
                registered.Add(action);
            }

            foreach (var provider in host.VariableProviders)
            {
                if (provider is IVariableCatalogSource source) catalog.Add(source);
                providerHost.StartOwned(manifest.Id, provider, variableStore);
            }

            var info = new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Loaded, null, host.SettingsPage is not null);
            var entry = new Entry(dir, info) { Running = new Running(context, instance, host, variableStore) };
            lock (_stateLock) _entries[manifest.Id] = entry;
            logger.LogInformation("Plugin loaded: {Id} {Version}", manifest.Id, manifest.Version);
            return info;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Plugin failed to load: {Id}", manifest.Id);

            // Roll back whatever was already applied so a failed plugin leaves nothing behind.
            foreach (var action in registered) dispatcher.Unregister(action);
            if (host is not null)
            {
                foreach (var source in host.VariableProviders.OfType<IVariableCatalogSource>()) catalog.Remove(source);
                await providerHost.StopOwnerAsync(manifest.Id, ProviderStopTimeout);
                DisposeAll(host, manifest.Id, instance);
            }
            else if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            statusRegistry.RemovePlugin(manifest.Id);
            variableStore.RemoveAll();
            context?.Unload();

            return Fail(PluginLoadStatus.Error, ex.Message);
        }
    }

    // ---- unloading ----

    /// <summary>Takes a running plugin's registrations back out, stops its providers, disposes what it created and
    /// unloads its assembly context. The entry stays in the list (as Unloaded → caller decides to drop or reload).</summary>
    private async Task UnloadCoreAsync(Entry entry)
    {
        var running = entry.Running;
        if (running is null) return;
        var id = entry.Info.Id;

        foreach (var action in running.Host.Actions) dispatcher.Unregister(action);
        foreach (var source in running.Host.VariableProviders.OfType<IVariableCatalogSource>()) catalog.Remove(source);

        await providerHost.StopOwnerAsync(id, ProviderStopTimeout);
        running.VariableStore.RemoveAll();

        DisposeAll(running.Host, id, running.Instance);
        statusRegistry.RemovePlugin(id);
        entry.Running = null;

        if (running.Context is { } context)
        {
            var weak = new WeakReference(context);
            context.Unload();
            await WaitForCollectionAsync(weak, id);
        }
        logger.LogInformation("Plugin unloaded: {Id}", id);
    }

    /// <summary>Disposes the plugin instance and everything it registered (each distinct object once), so a plugin
    /// that holds sockets, timers or threads can release them. Failures are logged and never stop the unload.</summary>
    private void DisposeAll(PluginHostCollector host, string id, IPlugin? instance = null)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        IEnumerable<object?> targets =
        [
            instance,
            .. host.Actions,
            .. host.VariableProviders,
            host.SettingsPage,
            .. host.IconPacks,
        ];

        foreach (var target in targets)
        {
            if (target is null || !seen.Add(target)) continue;
            try
            {
                switch (target)
                {
                    case IAsyncDisposable asyncDisposable:
                        asyncDisposable.DisposeAsync().AsTask().Wait(ProviderStopTimeout);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[{PluginId}] Dispose of {Type} failed", id, target.GetType().Name);
            }
        }
    }

    /// <summary>A collectible context only goes away once nothing references it (a lingering thread or timer
    /// inside the plugin keeps it alive). Gives the GC a few rounds; if it survives, logs it — the plugin is
    /// already fully deregistered, only its memory is held until the process ends.</summary>
    private async Task WaitForCollectionAsync(WeakReference weak, string id)
    {
        for (var i = 0; i < 10 && weak.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(50);
        }
        if (weak.IsAlive)
            logger.LogWarning("Plugin {Id} was deregistered but its assembly context is still referenced; its memory is released at the next restart", id);
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
