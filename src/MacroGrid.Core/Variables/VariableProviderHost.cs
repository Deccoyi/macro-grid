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
    // Owned providers stop with this token. It is cancelled in StopAsync and does not depend on when the runtime
    // gets around to calling ExecuteAsync: BackgroundService may start that call after StartAsync has returned
    // (seen on slower machines), which made StartOwned fail with "has not started yet".
    private readonly CancellationTokenSource _hostCts = new();
    private volatile bool _started;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _started = true;
        return base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _hostCts.Cancel();
        await base.StopAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(providers.Select(p => RunWithRestartAsync(p, store, stoppingToken)));

    /// <summary>Starts a plugin-owned provider. It writes through <paramref name="ownerStore"/> and stops with the
    /// application or when <see cref="StopOwnerAsync"/> is called for <paramref name="ownerId"/>.</summary>
    public void StartOwned(string ownerId, IVariableProvider provider, IVariableStore ownerStore)
    {
        // PluginManager is registered after this hosted service, so it has been started by the time plugins load.
        if (!_started)
            throw new InvalidOperationException("Variable provider host has not started yet.");

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_hostCts.Token);
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
