using System.Text.Json;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Plugins.Widgets;
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

    /// <summary>A development build that runs an unsigned C# plugin says so in the editor's status bar, in red, for as
    /// long as one is running.</summary>
    private void RefreshUnsignedNotice()
    {
        bool any;
        lock (_stateLock) any = _entries.Values.Any(e => e.Running is not null && e.Info.Unsigned);
        if (any)
            statusRegistry.SetCore("unsigned-plugins", "Development build: unsigned C# plugins are loaded", StatusLevel.Error,
                tooltip: "This server was built from source and runs C# plugins that are not officially signed. Released builds never do this.");
        else
            statusRegistry.RemoveCore("unsigned-plugins");
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

        var listedAsRevoked = officialCatalog?.Revoked?.Find(manifest.Id, manifest.Version);

        LoadedPlugin Fail(PluginLoadStatus status, string detail, IReadOnlyList<string>? pending = null, string? problemCode = null)
        {
            var info = new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, status, detail, false, pending);
            if (manifest.Widgets is { Length: > 0 })
                widgetCatalog?.Set(manifest.Id, manifest.Name, status switch
                {
                    PluginLoadStatus.NeedsApproval => PluginWidgetAvailability.NeedsApproval,
                    PluginLoadStatus.Incompatible => PluginWidgetAvailability.Incompatible,
                    _ => PluginWidgetAvailability.Disabled,
                }, false, [], []);
            else
                widgetCatalog?.Remove(manifest.Id);
            // Needs approval is not a fault: the Plugins window already asks for it.
            if (status is not (PluginLoadStatus.NeedsApproval or PluginLoadStatus.Disabled))
                problems?.Report(manifest.Id, manifest.Name, status == PluginLoadStatus.Incompatible ? ProblemSeverity.Warning : ProblemSeverity.Error,
                    problemCode ?? status switch { PluginLoadStatus.NotAllowed => ProblemCodes.NotAllowed, PluginLoadStatus.Incompatible => ProblemCodes.Incompatible, _ => ProblemCodes.LoadFailed }, detail);
            lock (_stateLock)
            {
                // A second folder with the same id must not replace the first one's entry.
                if (_entries.TryGetValue(manifest.Id, out var existing) && !string.Equals(existing.Dir, dir, StringComparison.OrdinalIgnoreCase))
                    _unrecognized.Add(info);
                else
                    _entries[manifest.Id] = new Entry(dir, info) { ListedAsRevoked = listedAsRevoked is not null };
            }
            return info;
        }

        if (GetPluginDir(manifest.Id) is { } takenBy && !string.Equals(takenBy, dir, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Plugin id clash: {Id} ({Folder})", manifest.Id, folderName);
            return Fail(PluginLoadStatus.Error, "This id is already used by another installed plugin");
        }

        // Switched off by the person: nothing of the plugin runs, and its variables, actions and widgets disappear.
        if (permissionStore.IsDisabled(manifest.Id))
            return Fail(PluginLoadStatus.Disabled, "Switched off in the Plugins window");

        // Only official, signed C# plugins run; checked at every load. Nothing of the plugin has executed before this point.
        PluginTrustResult? trust = null;
        if (manifest.Kind == PluginKind.Csharp)
        {
            trust = _trust.Verify(dir, manifest);
            if (!trust.Allowed)
            {
                logger.LogWarning(SecurityEvents.PluginNotAllowed, "Security: C# plugin {Id} was not loaded: {Reason}", SecurityEvents.ForLog(manifest.Id), trust.Reason);
                return Fail(PluginLoadStatus.NotAllowed, trust.Reason!);
            }
            if (trust.Unsigned)
                logger.LogWarning("C# plugin {Id} is not signed; loaded because this is a development build", SecurityEvents.ForLog(manifest.Id));
        }

        // An official JavaScript plugin that was installed with a contents signature is checked again at every load: an edit
        // in place (or a file added next to it) stops it until it is reinstalled. Other JavaScript plugins are not checked.
        var origin = origins?.Get(manifest.Id);
        var contentsVerified = false;
        if (manifest.Kind == PluginKind.Js && origin is { Trust: PluginTrust.Official, ContentsSigned: true })
        {
            contentsVerified = _trust.VerifyStrict(dir, manifest).Allowed;
            if (!contentsVerified)
            {
                logger.LogWarning(SecurityEvents.PluginFilesChanged, "Security: official plugin {Id} was not loaded: its files changed after install", SecurityEvents.ForLog(manifest.Id));
                return Fail(PluginLoadStatus.NotAllowed, "The plugin's files changed after it was installed. Reinstall it from the Plugins window.", problemCode: ProblemCodes.FilesChanged);
            }
        }
        problems?.Resolve(manifest.Id, ProblemCodes.FilesChanged);

        // The official safety list only reaches what the official project vouches for: plugins installed from the official
        // catalog, officially signed C# plugins and signed JavaScript folders. A saved list is all this reads; with none, everything loads.
        if (listedAsRevoked is not null
            && (origin?.Trust == PluginTrust.Official || contentsVerified || trust is { Allowed: true, Unsigned: false }))
        {
            logger.LogWarning(SecurityEvents.PluginRevoked, "Security: plugin {Id} {Version} was not loaded: switched off by the official plugin list",
                SecurityEvents.ForLog(manifest.Id), SecurityEvents.ForLog(manifest.Version));
            return Fail(PluginLoadStatus.NotAllowed, $"Switched off by the official plugin list: {listedAsRevoked.Reason}", problemCode: ProblemCodes.Revoked);
        }

        var compatibility = PluginCompatibility.Check(serverVersion, manifest.MinMacroGrid, manifest.MacroGrid, manifest.SdkVersion);
        if (!compatibility.Compatible)
            return Fail(PluginLoadStatus.Incompatible, compatibility.Reason!);

        var entryPath = ResolveEntryPath(dir, manifest.Entry);
        if (entryPath is null)
            return Fail(PluginLoadStatus.Error, $"Entry file not found: {manifest.Entry}");

        var variableStore = new TrackingVariableStore(variables);
        string[] declared = [.. manifest.Permissions ?? []];
        if (manifest.Kind == PluginKind.Js)
        {
            var unknown = declared.FirstOrDefault(p => !JsPermissions.IsKnown(p));
            if (unknown is not null)
                return Fail(PluginLoadStatus.Error, $"Unknown permission '{unknown}'");
        }

        // A C# plugin is verified when it is officially signed; a JavaScript plugin never is (its widgets get the stricter limits).
        var verified = manifest.Kind == PluginKind.Csharp && trust?.Unsigned != true;
        var widgetCheck = PluginWidgetValidator.Validate(dir, manifest, verified);
        foreach (var refused in widgetCheck.Problems)
        {
            logger.LogWarning("Plugin {Id}: widget '{Widget}' was left out: {Reason}", SecurityEvents.ForLog(manifest.Id), SecurityEvents.ForLog(refused.WidgetId), refused.Reason);
            problems?.Report(manifest.Id, manifest.Name, ProblemSeverity.Warning, ProblemCodes.WidgetInvalid, $"Widget '{refused.WidgetId}' was left out: {refused.Reason}");
        }
        if (widgetCheck.Problems.Count == 0) problems?.Resolve(manifest.Id, ProblemCodes.WidgetInvalid);

        // What the person approves: a JavaScript plugin's permissions plus every option a widget declares (keep loaded, storage).
        // Both kinds of plugin wait for approval; the C# ones only when they have widgets that declare options.
        string[] widgetKeys = [.. widgetCheck.Widgets.SelectMany(w => w.Options.Select(o => PluginWidgetOptions.ApprovalKey(w.Manifest.Id, o)))];
        string[] required = [.. declared, .. widgetKeys];
        if (required.Length > 0 && !permissionStore.IsGranted(manifest.Id, required))
            return Fail(PluginLoadStatus.NeedsApproval, "Needs your approval before it can run", required);

        // An approved widget option the person switched off is left out of the widget, so it runs without it.
        var switchedOff = permissionStore.SwitchedOff(manifest.Id);
        var widgets = widgetCheck.Widgets
            .Select(w => w with { Options = [.. w.Options.Where(o => !switchedOff.Contains(PluginWidgetOptions.ApprovalKey(w.Manifest.Id, o), StringComparer.OrdinalIgnoreCase))] })
            .ToList();

        PluginLoadContext? context = null;
        PluginHostCollector? host = null;
        var registered = new List<IActionHandler>();
        IPlugin? instance = null;
        try
        {
            if (manifest.Kind == PluginKind.Js)
            {
                instance = new JsPlugin(manifest, entryPath, new JsPermissions(declared.Except(permissionStore.SwitchedOff(manifest.Id), StringComparer.OrdinalIgnoreCase)), variableStore, input, logger,
                    reason => _ = Task.Run(() => DisableAsync(manifest.Id, reason)), windows: windowSource, problems: problems, network: new JsNetworkPolicy(ownPorts ?? []), notifications: notifications);
            }
            else
            {
                context = new PluginLoadContext(manifest.Id, entryPath, trust!.SignedFiles, dir);
                var assembly = context.LoadPluginAssembly(entryPath);
                var pluginType = assembly.GetTypes().FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                    ?? throw new InvalidOperationException($"No type implementing IPlugin was found in {manifest.Entry}");
                instance = (IPlugin)(Activator.CreateInstance(pluginType) ?? throw new InvalidOperationException("The plugin instance could not be created"));
            }

            host = new PluginHostCollector(serverVersion, dir, manifest.Id, statusRegistry, logger, secretProtector, widgetEvents, manifest.Name, problems);
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
            problems?.Resolve("macro-grid", ProblemCodes.ActionMissing);
            problems?.Resolve(manifest.Id, ProblemCodes.NotAllowed, ProblemCodes.Revoked, ProblemCodes.LoadFailed, ProblemCodes.Incompatible, ProblemCodes.SwitchedOff, ProblemCodes.PluginReported);
            var treeProvider = instance as IPluginTreeProvider;
            var info = new LoadedPlugin(manifest.Id, manifest.Name, manifest.Version, PluginLoadStatus.Loaded, null,
                host.SettingsPage is not null, HasIcon: ResolveIconPath(dir, manifest) is not null, HasTreeItems: treeProvider is not null,
                Unsigned: trust?.Unsigned == true,
                Permissions: required.Length > 0 ? required : null,
                SwitchedOffPermissions: required.Length > 0 ? [.. required.Where(p => switchedOff.Contains(p, StringComparer.OrdinalIgnoreCase))] : null);
            var running = new Running(context, instance, host, variableStore) { WidgetHandler = instance as IPluginWidgetHandler };
            if (manifest.Widgets is { Length: > 0 })
                widgetCatalog?.Set(manifest.Id, manifest.Name, PluginWidgetAvailability.Available, verified, widgets, widgetCheck.Problems, manifest.Kind);
            else
                widgetCatalog?.Remove(manifest.Id);
            if (treeProvider is not null)
            {
                var pluginId = manifest.Id;
                running.TreeChangedHandler = parentId => TreeChanges.Record(pluginId, parentId);
                treeProvider.TreeItemsChanged += running.TreeChangedHandler;
            }
            var entry = new Entry(dir, info) { Running = running, ListedAsRevoked = listedAsRevoked is not null };
            lock (_stateLock) _entries[manifest.Id] = entry;
            if (treeProvider is not null) TreeChanges.Record(manifest.Id, null, wholePlugin: true);
            RefreshUnsignedNotice();
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
