namespace MacroGrid.Core.Model;

/// <summary>A device that has completed PIN pairing at least once. <see cref="Token"/> is what it sends
/// in every future `hello` instead of the PIN — losing it (uninstall, cache clear) means pairing again.</summary>
public sealed class PairedDevice
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Token { get; set; }
    public DateTimeOffset PairedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Profile this device always opens, chosen in the editor's "Eşleştirme" device list. Null
    /// means "no explicit choice" — falls back to the first profile, same as before per-device assignment
    /// existed. Not validated against <c>ProfileStore</c> here: a dangling id (the assigned profile was
    /// deleted) is treated as unset by <c>ClientHub.OnHelloAsync</c>'s lookup, not an error.</summary>
    public string? AssignedProfileId { get; set; }

    /// <summary>Opt-in: whether this device's session auto-switches profile based on the foreground window
    /// on the server machine (docs/auto-profile-switch.md). Off by default — most devices never want this.</summary>
    public bool FollowActiveWindow { get; set; }

    /// <summary>User-set pause on auto-switching (the drawer's lock) — persisted so it survives a
    /// reconnect. Manual profile switches from the drawer/a button still work while locked.</summary>
    public bool AutoSwitchLocked { get; set; }
}
