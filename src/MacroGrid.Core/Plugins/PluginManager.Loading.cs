using System.Text.Json;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins;

public sealed partial class PluginManager
{
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
                if (provider is IVariableCatalogSource source) catalog.Add(source, manifest.Id);
                providerHost.StartOwned(manifest.Id, provider, variableStore);
            }

            localizer?.Register(manifest.Id, dir, manifest.DefaultLanguage);
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

}
