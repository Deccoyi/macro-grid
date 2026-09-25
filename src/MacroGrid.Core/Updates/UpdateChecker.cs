using System.Text.Json;

namespace MacroGrid.Core.Updates;

public enum UpdateCheckOutcome
{
    /// <summary>The feed could not be read (offline, rate-limited, malformed); <see cref="UpdateCheckResult.Error"/> says why.</summary>
    Failed,
    UpToDate,
    Available,
}

/// <param name="Offer">The update to offer, when <see cref="Outcome"/> is <see cref="UpdateCheckOutcome.Available"/>.</param>
/// <param name="Announce">Whether the policy wants the person told now (a notification, or for a manual check the update window).</param>
public sealed record UpdateCheckResult(UpdateCheckOutcome Outcome, UpdateOffer? Offer = null, bool Announce = false, string? Error = null);

/// <summary>
/// One update check from end to end: fetch the feed (or reuse the cached list on a 304), remember the ETag, find the update, and ask the
/// policy whether to announce it. No timer and no UI; the host's update service decides when to call it and what to do with the result.
/// </summary>
public sealed class UpdateChecker(ReleaseFeed feed, UpdateStateStore store, UpdatePolicy policy, ReleaseVersion current, TimeProvider clock)
{
    public async Task<UpdateCheckResult> CheckAsync(bool manual, bool automaticChecksEnabled, bool includePreReleases, CancellationToken cancellationToken)
    {
        IReadOnlyList<ReleaseInfo> releases;
        try
        {
            releases = await LoadReleasesAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
            && ex is HttpRequestException or JsonException or IOException or TaskCanceledException)
        {
            return new UpdateCheckResult(UpdateCheckOutcome.Failed, Error: ex.Message);
        }

        store.Update(s => s with { LastCheckUtc = clock.GetUtcNow() });

        var offer = ReleaseFeed.FindUpdate(releases, current, includePreReleases);
        if (offer is null) return new UpdateCheckResult(UpdateCheckOutcome.UpToDate);

        var announce = policy.ShouldNotify(store.Get(), offer.Latest.Version, automaticChecksEnabled, manual);
        if (announce) store.Update(s => policy.MarkNotified(s, offer.Latest.Version));
        return new UpdateCheckResult(UpdateCheckOutcome.Available, offer, announce);
    }

    private async Task<IReadOnlyList<ReleaseInfo>> LoadReleasesAsync(CancellationToken cancellationToken)
    {
        var state = store.Get();
        // An ETag is only worth sending while the list that went with it is still stored.
        var response = await feed.FetchAsync(state.FeedJson is null ? null : state.ETag, cancellationToken);

        if (response.NotModified) return ReleaseFeed.Parse(state.FeedJson ?? "[]");

        store.Update(s => s with { ETag = response.ETag, FeedJson = response.Body });
        return ReleaseFeed.Parse(response.Body!);
    }
}
