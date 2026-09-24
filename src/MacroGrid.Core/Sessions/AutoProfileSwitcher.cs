using MacroGrid.Core.Devices;
using MacroGrid.Core.Model;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Sessions;

/// <summary>
/// Reacts to <see cref="IActiveWindowSource"/> foreground-window changes by driving each opted-in
/// session's <see cref="AutoSwitchState"/> and applying the result — see docs/auto-profile-switch.md. The
/// stack/lock logic itself lives in the pure <see cref="AutoSwitchState"/>; this class is just the glue
/// between the Windows event source, <see cref="ProfileStore"/>'s <c>AppMatch</c> rules, and the device's
/// own opt-in (<see cref="PairedDevice.FollowActiveWindow"/>).
/// </summary>
public sealed class AutoProfileSwitcher(
    IActiveWindowSource windowSource,
    SessionRegistry sessions,
    ProfileStore profiles,
    DeviceStore devices,
    PreferencesStore preferences,
    WidgetStateService widgetState,
    ILogger<AutoProfileSwitcher> logger) : BackgroundService
{
    /// <summary>How often a dead process is pruned from a session's stack while at least one session has
    /// a live Rule entry — "Yığında kural yokken timer çalışmaz" means this loop is cheap to just always
    /// run; the work inside is skipped instantly when nothing has a Rule entry.</summary>
    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        windowSource.ForegroundChanged += OnForegroundChanged;
        windowSource.Start();
        try
        {
            using var timer = new PeriodicTimer(PruneInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await PruneAllAsync();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            windowSource.ForegroundChanged -= OnForegroundChanged;
        }
    }

    /// <summary>Re-applies the current foreground window to one session right after it unlocks —
    /// "kilit açıldığında durum bir kez yeniden değerlendirilir". A no-op for a session that isn't
    /// opted in or has no known current foreground window yet.</summary>
    public async Task ReevaluateAsync(ClientSession session)
    {
        if (FindDevice(session.DeviceId) is not { FollowActiveWindow: true } device) return;
        if (_lastForeground is not { } window) return;
        await ApplyAsync(session, device, window);
    }

    private ForegroundWindow? _lastForeground;

    private void OnForegroundChanged(ForegroundWindow window)
    {
        _lastForeground = window;
        _ = OnForegroundChangedAsync(window);
    }

    private async Task OnForegroundChangedAsync(ForegroundWindow window)
    {
        foreach (var session in sessions.All)
        {
            if (FindDevice(session.DeviceId) is not { FollowActiveWindow: true } device) continue;
            try
            {
                await ApplyAsync(session, device, window);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto profile switch failed for session {Session}", session.Id);
            }
        }
    }

    private async Task PruneAllAsync()
    {
        foreach (var session in sessions.All)
        {
            if (FindDevice(session.DeviceId) is not { FollowActiveWindow: true } device) continue;
            try
            {
                var result = session.AutoSwitch.Prune(windowSource.HasVisibleWindow);
                await HandleResultAsync(session, device, result);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Auto profile switch prune failed for session {Session}", session.Id);
            }
        }
    }

    private async Task ApplyAsync(ClientSession session, PairedDevice device, ForegroundWindow window)
    {
        var profileId = ResolveProfileFor(window);
        var result = session.AutoSwitch.OnForeground(window.ProcessName, profileId);
        await HandleResultAsync(session, device, result);
    }

    private async Task HandleResultAsync(ClientSession session, PairedDevice device, AutoSwitchResult result)
    {
        if (!result.Changed) return;
        var targetId = result.ToDefault
            ? ProfileResolver.ResolveDefault(device, profiles, preferences).Id
            : result.ProfileId;
        if (targetId is null || targetId == session.ProfileId) return;

        await new SessionDeviceController(session, profiles, widgetState).ApplyProfileAsync(targetId);
    }

    /// <summary>First profile (in <see cref="ProfileStore.All"/> order — name-sorted) whose <c>AppMatches</c>
    /// matches this window. Null for an undefined window — "tanımsız pencere → hiçbir şey olmaz".</summary>
    private string? ResolveProfileFor(ForegroundWindow window) =>
        profiles.All.FirstOrDefault(p => p.AppMatches.Any(m => Matches(m, window)))?.Id;

    private static bool Matches(AppMatch match, ForegroundWindow window) =>
        string.Equals(match.ProcessName.Trim(), window.ProcessName, StringComparison.OrdinalIgnoreCase)
        && (string.IsNullOrEmpty(match.TitleContains) || window.Title.Contains(match.TitleContains, StringComparison.OrdinalIgnoreCase));

    private PairedDevice? FindDevice(string? deviceId) =>
        deviceId is null ? null : devices.All.FirstOrDefault(d => d.Id == deviceId);
}
