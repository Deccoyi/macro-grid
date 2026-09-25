namespace MacroGrid.Core.Updates;

/// <summary>The installer file of a release: where to get it and the SHA-256 GitHub reports for it.</summary>
/// <param name="Sha256">Lower-case hex, or null when the API did not report a digest.</param>
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string? Sha256);

/// <summary>One published server release, as the updater needs it.</summary>
/// <param name="PreReleaseFlag">GitHub's own pre-release flag for the release; a release also counts as a pre-release when its version has a label.</param>
/// <param name="Notes">The release body (Markdown).</param>
/// <param name="PageUrl">The release page on GitHub (null when the API gave none the updater accepts).</param>
/// <param name="Installer">The <c>MacroGrid-Setup-&lt;version&gt;.exe</c> asset, or null when the release has none or its URL is refused.</param>
public sealed record ReleaseInfo(
    ReleaseVersion Version,
    string Tag,
    string Name,
    string Notes,
    DateTimeOffset? PublishedAt,
    Uri? PageUrl,
    ReleaseAsset? Installer,
    bool PreReleaseFlag = false)
{
    public bool IsPreRelease => PreReleaseFlag || Version.IsPreRelease;

    /// <summary>"Install now" needs the installer and a digest to verify it against; without both, the person gets the release page.</summary>
    public bool CanInstall => Installer is { Sha256: not null };
}

/// <summary>The newest release that is newer than the running version, with every release in between (newest first) for the release notes.</summary>
public sealed record UpdateOffer(ReleaseInfo Latest, IReadOnlyList<ReleaseInfo> Included);
