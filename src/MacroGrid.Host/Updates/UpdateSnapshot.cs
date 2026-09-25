namespace MacroGrid.Host.Updates;

/// <summary>What <c>GET /api/update</c> tells the editor: the running version, whether a newer one exists and what changed.</summary>
/// <param name="LastCheckedAt">When the release list was last read successfully; null before the first check.</param>
/// <param name="Error">Why the last check failed, or null.</param>
/// <param name="Install">The state of "Install now"; filled in by the API, null when read from the update service alone.</param>
internal sealed record UpdateSnapshot(string CurrentVersion, DateTimeOffset? LastCheckedAt, bool Checking, string? Error, UpdateSnapshot.AvailableUpdate? Available, UpdateSnapshot.InstallStatus? Install = null)
{
    /// <param name="CanInstall">Whether "Install now" works for this release (needs the installer and its digest).</param>
    /// <param name="Skipped">The person chose "Skip this version" for it.</param>
    /// <param name="Releases">Every release between the running version and this one, newest first.</param>
    public sealed record AvailableUpdate(string Version, string Name, string? PageUrl, bool CanInstall, bool Skipped, IReadOnlyList<ReleaseNote> Releases);

    /// <param name="State">"idle", "downloading", "starting" (the setup is open, the person finishes it there), "cancelled" (the setup ended without installing; nothing changed) or "failed".</param>
    /// <param name="Percent">Download progress, 0 to 100.</param>
    /// <param name="Error">For "failed": "declined" (the person said no to the administrator prompt), "refused", "download", "verify" or "start".</param>
    public sealed record InstallStatus(string State, int Percent, string? Error);

    public sealed record ReleaseNote(string Version, string Name, string Notes, DateTimeOffset? PublishedAt);
}
