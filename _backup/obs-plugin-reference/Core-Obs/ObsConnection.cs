using System.Text.Json.Nodes;
using MacroStation.Core.Preferences;
using MacroStation.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace MacroStation.Core.Obs;

/// <summary>
/// Owns the (re)connecting OBS WebSocket connection for the whole process lifetime, following the same
/// shape as <see cref="MacroStation.Windows.Variables.SystemMetricsProvider"/>: an <see cref="IVariableProvider"/>
/// that never returns while the process is alive, publishing "obs.*" variables and reconnecting with capped
/// exponential backoff whenever OBS isn't running or the connection drops. Action handlers
/// (<see cref="ObsActions"/>) share this same instance via <see cref="RequestAsync"/> to actually do things —
/// there's exactly one connection to OBS, actions don't open their own.
/// </summary>
public sealed class ObsConnection(PreferencesStore preferences, ILogger<ObsConnection> logger)
    : IVariableProvider, IVariableCatalogSource
{
    private const string Category = "OBS";

    private readonly Lock _clientLock = new();
    private ObsClient? _client;

    public IEnumerable<VariableInfo> Describe() =>
    [
        new("obs.connected", "OBS'e bağlı mı", "{obs.connected}", Category),
        new("obs.streaming", "Yayın açık mı", "{obs.streaming}", Category),
        new("obs.stream.duration", "Yayın süresi", "{obs.stream.duration}", Category),
        new("obs.recording", "Kayıt açık mı", "{obs.recording}", Category),
        new("obs.record.duration", "Kayıt süresi", "{obs.record.duration}", Category),
        new("obs.scene.current", "Aktif sahne adı", "{obs.scene.current}", Category),
        new("obs.stats.fps", "OBS render FPS", "{obs.stats.fps|0}", Category),
        new("obs.stats.cpu", "OBS CPU kullanımı (%)", "{obs.stats.cpu|0}%", Category),
    ];

    /// <summary>Used by <see cref="ObsActions"/>. Throws <see cref="InvalidOperationException"/> if not currently connected — actions
    /// don't queue or wait for a connection, a button press while OBS is closed should fail fast and visibly (logged by ActionDispatcher).</summary>
    public Task<JsonObject> RequestAsync(string requestType, JsonObject? requestData, CancellationToken cancellationToken)
    {
        ObsClient? client;
        lock (_clientLock) client = _client;
        if (client is null)
            throw new InvalidOperationException("OBS'e bağlı değil.");
        return client.RequestAsync(requestType, requestData, cancellationToken);
    }

    public async Task RunAsync(IVariableStore store, CancellationToken cancellationToken)
    {
        var backoff = TimeSpan.FromSeconds(2);
        SetDisconnected(store);

        while (!cancellationToken.IsCancellationRequested)
        {
            var settings = preferences.Get().Obs;
            if (!settings.Enabled)
            {
                if (!await DelayAsync(TimeSpan.FromSeconds(5), cancellationToken)) break;
                continue;
            }

            ObsClient? client = null;
            try
            {
                client = await ObsClient.ConnectAsync(settings.Host, settings.Port, settings.Password, cancellationToken);
                lock (_clientLock) _client = client;
                logger.LogInformation("OBS'e bağlanıldı ({Host}:{Port})", settings.Host, settings.Port);
                store.Set("obs.connected", true);
                backoff = TimeSpan.FromSeconds(2);

                var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                client.Disconnected += _ => disconnected.TrySetResult();
                client.EventReceived += (type, data) => HandleEvent(store, type, data);

                await RefreshStatusAsync(client, store);

                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (!disconnected.Task.IsCompleted)
                {
                    var tick = timer.WaitForNextTickAsync(cancellationToken).AsTask();
                    if (await Task.WhenAny(tick, disconnected.Task) == disconnected.Task) break;
                    if (!await tick) break;
                    await RefreshStatusAsync(client, store);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "OBS'e bağlanılamadı, {Delay}s sonra tekrar denenecek", backoff.TotalSeconds);
            }
            finally
            {
                lock (_clientLock) _client = null;
                if (client is not null) await client.DisposeAsync();
                SetDisconnected(store);
            }

            if (!await DelayAsync(backoff, cancellationToken)) break;
            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 30));
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try { await Task.Delay(delay, cancellationToken); return true; }
        catch (OperationCanceledException) { return false; }
    }

    private static void SetDisconnected(IVariableStore store)
    {
        store.Set("obs.connected", false);
        store.Set("obs.streaming", false);
        store.Set("obs.recording", false);
    }

    private static void HandleEvent(IVariableStore store, string eventType, JsonObject data)
    {
        switch (eventType)
        {
            case "CurrentProgramSceneChanged":
                store.Set("obs.scene.current", data["sceneName"]?.GetValue<string>());
                break;
            case "StreamStateChanged":
                store.Set("obs.streaming", data["outputActive"]?.GetValue<bool>() ?? false);
                break;
            case "RecordStateChanged":
                store.Set("obs.recording", data["outputActive"]?.GetValue<bool>() ?? false);
                break;
        }
    }

    /// <summary>Polls the request-only fields (durations, fps, current scene) once a second — obs-websocket
    /// only pushes these via events for the boolean active/inactive edges, not their running values.</summary>
    private async Task RefreshStatusAsync(ObsClient client, IVariableStore store)
    {
        try
        {
            var stream = await client.RequestAsync("GetStreamStatus", null, CancellationToken.None);
            store.Set("obs.streaming", stream["outputActive"]?.GetValue<bool>() ?? false);
            store.Set("obs.stream.duration", TimeSpan.FromMilliseconds(stream["outputDuration"]?.GetValue<double>() ?? 0));

            var record = await client.RequestAsync("GetRecordStatus", null, CancellationToken.None);
            store.Set("obs.recording", record["outputActive"]?.GetValue<bool>() ?? false);
            store.Set("obs.record.duration", TimeSpan.FromMilliseconds(record["outputDuration"]?.GetValue<double>() ?? 0));

            var stats = await client.RequestAsync("GetStats", null, CancellationToken.None);
            store.Set("obs.stats.fps", stats["activeFps"]?.GetValue<double>() ?? 0);
            store.Set("obs.stats.cpu", stats["cpuUsage"]?.GetValue<double>() ?? 0);

            var scene = await client.RequestAsync("GetCurrentProgramScene", null, CancellationToken.None);
            store.Set("obs.scene.current", scene["sceneName"]?.GetValue<string>());
        }
        catch (ObsRequestException ex)
        {
            logger.LogDebug(ex, "OBS durum sorgusu başarısız (muhtemelen geçici)");
        }
    }
}
