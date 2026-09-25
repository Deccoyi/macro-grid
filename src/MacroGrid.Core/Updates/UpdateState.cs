namespace MacroGrid.Core.Updates;

/// <summary>What the updater remembers between runs (<c>update-state.json</c>). Kept apart from the preferences because the editor saves those as a whole object.</summary>
/// <param name="LastCheckUtc">When the feed was last fetched successfully.</param>
/// <param name="ETag">The releases list's ETag, sent back as <c>If-None-Match</c>.</param>
/// <param name="FeedJson">The releases list that went with <paramref name="ETag"/>, so a 304 still has something to read.</param>
/// <param name="SnoozedUntilUtc">"Later": no notification before this time.</param>
/// <param name="SkippedVersion">"Skip this version": no notification for exactly this version.</param>
/// <param name="NotifiedVersion">The version the person was last notified about, so the 6-hour checks do not repeat the notification.</param>
public sealed record UpdateState(
    DateTimeOffset? LastCheckUtc = null,
    string? ETag = null,
    string? FeedJson = null,
    DateTimeOffset? SnoozedUntilUtc = null,
    string? SkippedVersion = null,
    string? NotifiedVersion = null);
