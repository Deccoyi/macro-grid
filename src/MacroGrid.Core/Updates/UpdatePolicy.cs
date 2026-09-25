namespace MacroGrid.Core.Updates;

/// <summary>
/// Decides whether an available update should be announced (a notification), and records what the person chose. Pure over
/// <see cref="UpdateState"/> and an injected clock: every method returns the new state and persists nothing.
/// The status-bar item does not go through this; it stays as long as an update exists.
/// </summary>
public sealed class UpdatePolicy(TimeProvider clock)
{
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    public static readonly TimeSpan SnoozeDuration = TimeSpan.FromHours(24);

    /// <summary>
    /// A manual check always announces. An automatic one announces only when automatic checks are on, the version is not skipped, the
    /// snooze is over and this version has not been announced already.
    /// </summary>
    public bool ShouldNotify(UpdateState state, ReleaseVersion latest, bool automaticChecksEnabled, bool manual)
    {
        if (manual) return true;
        if (!automaticChecksEnabled) return false;
        if (IsSkipped(state, latest)) return false;
        if (state.SnoozedUntilUtc is { } until && clock.GetUtcNow() < until) return false;
        return state.NotifiedVersion != latest.ToString();
    }

    public static bool IsSkipped(UpdateState state, ReleaseVersion version) => state.SkippedVersion == version.ToString();

    public UpdateState MarkNotified(UpdateState state, ReleaseVersion version) => state with { NotifiedVersion = version.ToString() };

    /// <summary>"Later": quiet for 24 hours, then announced again on the next check.</summary>
    public UpdateState Snooze(UpdateState state) =>
        state with { SnoozedUntilUtc = clock.GetUtcNow() + SnoozeDuration, NotifiedVersion = null };

    /// <summary>"Skip this version": this exact version never notifies again; a newer one does.</summary>
    public static UpdateState Skip(UpdateState state, ReleaseVersion version) =>
        state with { SkippedVersion = version.ToString() };
}
