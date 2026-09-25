namespace MacroGrid.Core.Sessions;

/// <summary>Where a profile switch came from — a plain user pick (drawer, `core.profile` button) or the
/// auto-switcher reacting to the foreground window. See <see cref="AutoSwitchState"/>.</summary>
public enum ProfileSwitchOrigin { Manual, Auto }

/// <summary>One entry in a session's auto-switch stack. <see cref="ProcessName"/> is null for a
/// <see cref="ProfileSwitchOrigin.Manual"/> entry — it never gets pruned by a process closing.</summary>
public readonly record struct AutoSwitchEntry(string ProfileId, ProfileSwitchOrigin Source, string? ProcessName);

/// <summary>What a state transition asks the caller to do: nothing, switch to a specific profile, or
/// (the stack ran empty) fall back to whatever the caller's own default-profile resolution picks.</summary>
public readonly record struct AutoSwitchResult(bool Changed, bool ToDefault, string? ProfileId)
{
    public static readonly AutoSwitchResult Unchanged = new(false, false, null);
    public static AutoSwitchResult SwitchTo(string profileId) => new(true, false, profileId);
    public static readonly AutoSwitchResult SwitchToDefault = new(true, true, null);
}

/// <summary>
/// Pure per-session "which profile should be showing" stack — see docs/auto-profile-switch.md. Knows
/// nothing about ProfileStore, plugin-provided live state, or the actual foreground window; it is
/// only ever told "process X came to the foreground and resolves to profile Y" or "prune anything whose
/// process no longer has a visible window", by <see cref="AutoProfileSwitcher"/>. Kept side-effect-free
/// so the whole stack/lock/manual-base interplay (docs/auto-profile-switch.md's "Behavior model") can be
/// unit tested without any Windows dependency or live session.
/// </summary>
public sealed class AutoSwitchState
{
    private readonly List<AutoSwitchEntry> _stack = [];

    public bool Locked { get; private set; }

    /// <summary>Bottom-to-top order — the last entry is the one currently "showing". Exposed for tests.</summary>
    public IReadOnlyList<AutoSwitchEntry> Entries => _stack;

    /// <summary>A window came to the foreground. A defined one (an <c>AppMatch</c> resolved to
    /// <paramref name="profileId"/>) pushes its rule entry; an undefined one (null) drops every Auto entry, so
    /// the session falls back to the manual base (or the default) — the profile follows focus, not whether
    /// the app is merely still open.</summary>
    public AutoSwitchResult OnForeground(string processName, string? profileId)
    {
        if (Locked) return AutoSwitchResult.Unchanged;
        if (profileId is null) return DropAutoEntries();

        var existingIndex = _stack.FindIndex(e => e.Source == ProfileSwitchOrigin.Auto && e.ProcessName == processName);
        AutoSwitchEntry entry;
        if (existingIndex >= 0)
        {
            entry = _stack[existingIndex] with { ProfileId = profileId }; // a profile's rules may have changed since it was pushed
            var wasAlreadyTop = existingIndex == _stack.Count - 1 && entry.ProfileId == _stack[existingIndex].ProfileId;
            _stack.RemoveAt(existingIndex);
            _stack.Add(entry);
            if (wasAlreadyTop) return AutoSwitchResult.Unchanged;
        }
        else
        {
            entry = new AutoSwitchEntry(profileId, ProfileSwitchOrigin.Auto, processName);
            _stack.Add(entry);
        }

        return AutoSwitchResult.SwitchTo(entry.ProfileId);
    }

    /// <summary>Drops every <see cref="ProfileSwitchOrigin.Auto"/> entry whose process no longer has a
    /// visible window (a Manual base entry is never touched). Call this whenever a process might have
    /// closed — the caller decides when that's worth checking (see <c>AutoProfileSwitcher</c>'s polling
    /// vs. per-foreground-event pruning).</summary>
    public AutoSwitchResult Prune(Func<string, bool> hasVisibleWindow) =>
        RemoveAutoEntries(e => !hasVisibleWindow(e.ProcessName!));

    private AutoSwitchResult DropAutoEntries() => RemoveAutoEntries(_ => true);

    private AutoSwitchResult RemoveAutoEntries(Func<AutoSwitchEntry, bool> shouldRemove)
    {
        if (Locked || _stack.Count == 0) return AutoSwitchResult.Unchanged;

        var topBefore = _stack[^1];
        var removed = _stack.RemoveAll(e => e.Source == ProfileSwitchOrigin.Auto && shouldRemove(e));
        if (removed == 0) return AutoSwitchResult.Unchanged;

        if (_stack.Count == 0) return AutoSwitchResult.SwitchToDefault;
        var topAfter = _stack[^1];
        return topAfter.ProfileId == topBefore.ProfileId ? AutoSwitchResult.Unchanged : AutoSwitchResult.SwitchTo(topAfter.ProfileId);
    }

    /// <summary>A plain user pick (drawer, `core.profile` button) — always pushed as a new base entry, so
    /// a later rule match layers on top of it and returning to "nothing matches" comes back to this pick,
    /// not further down the stack.</summary>
    public void OnManual(string profileId) => _stack.Add(new AutoSwitchEntry(profileId, ProfileSwitchOrigin.Manual, null));

    /// <summary>Locking/unlocking never changes the stack itself — on unlock, the caller re-checks the
    /// live foreground window and calls <see cref="OnForeground"/>/<see cref="Prune"/> again ("kilit
    /// the state is re-evaluated once when the lock is released").</summary>
    public void SetLocked(bool locked) => Locked = locked;

    public string? CurrentProfileId => _stack.Count > 0 ? _stack[^1].ProfileId : null;
}
