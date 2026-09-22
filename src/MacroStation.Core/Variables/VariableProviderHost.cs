using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Variables;

/// <summary>
/// Runs every registered <see cref="IVariableProvider"/> for the process lifetime.
/// A provider that throws is restarted after a short delay instead of taking the others (or the server) down.
/// </summary>
public sealed class VariableProviderHost(IEnumerable<IVariableProvider> providers, VariableStore store, ILogger<VariableProviderHost> logger)
    : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(providers.Select(p => RunWithRestartAsync(p, stoppingToken)));

    private async Task RunWithRestartAsync(IVariableProvider provider, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await provider.RunAsync(store, ct);
                return; // a provider that returns normally is done for good; don't spin on it
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Variable provider {Provider} crashed, restarting in 5s", provider.GetType().Name);
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
