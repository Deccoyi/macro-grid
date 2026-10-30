using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Core.Preferences;

namespace MacroGrid.Host.Updates;

/// <summary>
/// The background look at the official plugin catalog, next to <see cref="UpdateService"/>. It runs only when "Check for updates automatically" is
/// on and at least one installed plugin comes from the official catalog. The first look is a few minutes after start with a random part, then
/// each file on its own schedule (see <see cref="PluginCatalogPolicy"/>). What happens in one step is <see cref="PluginCatalogMonitor"/>.
/// </summary>
internal sealed class PluginCatalogService(PluginCatalogMonitor monitor, PreferencesStore preferences, ILogger<PluginCatalogService> log) : BackgroundService
{
    private static readonly TimeSpan IdleWait = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The warning lines come from the saved copies, so they are right from the start without any network.
            monitor.UpdateLines();
            var start = PluginCatalogPolicy.StartDelay + TimeSpan.FromMinutes(Random.Shared.NextDouble() * PluginCatalogPolicy.StartJitter.TotalMinutes);
            await Task.Delay(start, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var wait = IdleWait;
                if (preferences.Get().CheckForUpdates && monitor.HasWatchedPlugins())
                {
                    await monitor.RunOnceAsync(stoppingToken);
                    wait = monitor.NextWait();
                }
                await Task.Delay(wait, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "The plugin catalog check stopped");
        }
    }
}
