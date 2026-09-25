using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Updates;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Host.Updates;

/// <summary>
/// Runs the update checks: one about a minute after start, then every few hours, and on request. It keeps the newest offer, shows it as the
/// "update" status item and tells the tray when a new version should be announced. A failed check is logged and retried at the next interval;
/// nothing is shown to the person for it (only a manual check reports it).
/// </summary>
internal sealed class UpdateService(
    UpdateChecker checker,
    UpdateStateStore state,
    UpdatePolicy policy,
    PreferencesStore preferences,
    PluginStatusRegistry status,
    ILogger<UpdateService> log) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile UpdateOffer? _offer;
    private volatile string? _error;
    private volatile bool _checking;

    /// <summary>Raised on a background thread when an automatic check found a version the person should be told about.</summary>
    public event Action<UpdateOffer>? UpdateAnnounced;

    public UpdateOffer? Offer => _offer;

    public UpdateSnapshot Snapshot()
    {
        var offer = _offer;
        var available = offer is null
            ? null
            : new UpdateSnapshot.AvailableUpdate(
                offer.Latest.Version.ToString(),
                offer.Latest.Name,
                offer.Latest.PageUrl?.AbsoluteUri,
                offer.Latest.CanInstall,
                UpdatePolicy.IsSkipped(state.Get(), offer.Latest.Version),
                offer.Included.Select(r => new UpdateSnapshot.ReleaseNote(r.Version.ToString(), r.Name, r.Notes, r.PublishedAt)).ToList());
        return new UpdateSnapshot(ClientHub.ServerVersion, state.Get().LastCheckUtc, _checking, _error, available);
    }

    /// <summary>A check the person asked for: it ignores "Later", "Skip" and the automatic-check switch.</summary>
    public Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken) => RunCheckAsync(manual: true, cancellationToken);

    /// <summary>"Later": no notification for 24 hours. False when there is no update to postpone.</summary>
    public bool Snooze()
    {
        if (_offer is null) return false;
        state.Update(policy.Snooze);
        return true;
    }

    /// <summary>"Skip this version": no notification for the offered version again. False when there is no update.</summary>
    public bool Skip()
    {
        if (_offer is not { } offer) return false;
        state.Update(s => UpdatePolicy.Skip(s, offer.Latest.Version));
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(UpdatePolicy.StartupDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                if (preferences.Get().CheckForUpdates) await RunCheckAsync(manual: false, stoppingToken);
                await Task.Delay(UpdatePolicy.CheckInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Stopping.
        }
    }

    private async Task<UpdateCheckResult> RunCheckAsync(bool manual, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _checking = true;
            var prefs = preferences.Get();
            var result = await checker.CheckAsync(manual, prefs.CheckForUpdates, prefs.IncludePreReleases, cancellationToken);

            if (result.Outcome == UpdateCheckOutcome.Failed)
            {
                _error = result.Error;
                log.LogWarning("Update check failed: {Error}", result.Error);
                return result;
            }

            _error = null;
            _offer = result.Offer;
            ShowStatus(result.Offer);
            if (result.Offer is not null)
                log.LogInformation("Update available: {Version}", result.Offer.Latest.Version);

            if (!manual && result.Announce && result.Offer is { } offer) UpdateAnnounced?.Invoke(offer);
            return result;
        }
        finally
        {
            _checking = false;
            _gate.Release();
        }
    }

    /// <summary>The status-bar item stays for as long as an update exists, also after "Later" or "Skip".</summary>
    private void ShowStatus(UpdateOffer? offer)
    {
        if (offer is null) status.RemoveCore("update");
        else status.SetCore("update", "Update available", StatusLevel.Ok, "download", offer.Latest.Version.ToString());
    }

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }
}
