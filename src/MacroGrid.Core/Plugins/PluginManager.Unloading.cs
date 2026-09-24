using MacroGrid.Core.Actions;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Plugins.Js;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Plugins;

public sealed partial class PluginManager
{
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
        localizer?.Unregister(id);
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
}
