using System.Collections.Concurrent;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Variables;

/// <summary>
/// Runs every registered <see cref="IVariableProvider"/> for the process lifetime.
/// A provider that throws is restarted after a short delay instead of taking the others (or the server) down.
/// Providers contributed by plugins are started and stopped at runtime, per owner, so a plugin can be
/// hot-loaded or unloaded without restarting the server.
/// </summary>
public sealed class VariableProviderHost(IEnumerable<IVariableProvider> providers, VariableStore store, ILogger<VariableProviderHost> logger)
    : BackgroundService
{
    private sealed record Running(CancellationTokenSource Cts, Task Task);

    private readonly ConcurrentDictionary<string, List<Running>> _owned = new();
    private readonly TaskCompletionSource<CancellationToken> _stopping = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stopping.TrySetResult(stoppingToken);
        return Task.WhenAll(providers.Select(p => RunWithRestartAsync(p, store, stoppingToken)));
    }

    /// <summary>Starts a plugin-owned provider. It writes through <paramref name="ownerStore"/> and stops with the
    /// application or when <see cref="StopOwnerAsync"/> is called for <paramref name="ownerId"/>.</summary>
    public void StartOwned(string ownerId, IVariableProvider provider, IVariableStore ownerStore)
    {
        // The host token exists as soon as this hosted service has started; PluginManager is registered after it.
        if (!_stopping.Task.IsCompletedSuccessfully)
            throw new InvalidOperationException("Variable provider host has not started yet.");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Task.Result);
        var running = new Running(cts, Task.Run(() => RunWithRestartAsync(provider, ownerStore, cts.Token)));
        var list = _owned.GetOrAdd(ownerId, _ => []);
        lock (list) list.Add(running);
    }

    /// <summary>Cancels every provider the owner started and waits (bounded) for them to finish.</summary>
    public async Task StopOwnerAsync(string ownerId, TimeSpan timeout)
    {
        if (!_owned.TryRemove(ownerId, out var list)) return;

        Running[] running;
        lock (list) running = [.. list];
        foreach (var r in running) r.Cts.Cancel();

        var all = Task.WhenAll(running.Select(r => r.Task));
        if (await Task.WhenAny(all, Task.Delay(timeout)) != all)
            logger.LogWarning("Variable providers of {Owner} did not stop within {Timeout}", ownerId, timeout);
        foreach (var r in running) r.Cts.Dispose();
    }

    private async Task RunWithRestartAsync(IVariableProvider provider, IVariableStore providerStore, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await provider.RunAsync(providerStore, ct);
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
