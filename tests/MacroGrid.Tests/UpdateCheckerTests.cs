using System.Net;
using System.Net.Http.Headers;
using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class UpdateCheckerTests : IDisposable
{
    private static readonly Uri Endpoint = new("https://api.github.com/repos/Deccoyi/macro-grid/releases");
    private static readonly ReleaseVersion Current = new(0, 2, 1);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private static string Feed(params string[] tags) =>
        "[" + string.Join(",", tags.Select(t => $"{{\"tag_name\":\"{t}\",\"body\":\"notes\",\"prerelease\":true,\"draft\":false}}")) + "]";

    private static HttpResponseMessage Ok(string body, string etag = "\"e1\"")
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        response.Headers.ETag = new EntityTagHeaderValue(etag);
        return response;
    }

    private (UpdateChecker Checker, UpdateStateStore Store, FakeHttpHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpHandler(respond);
        var store = new UpdateStateStore(_dir);
        var checker = new UpdateChecker(new ReleaseFeed(new HttpClient(handler), Endpoint), store, new UpdatePolicy(TimeProvider.System), Current, TimeProvider.System);
        return (checker, store, handler);
    }

    [Fact]
    public async Task Newer_release_is_available_and_announced_once()
    {
        var (checker, store, _) = Create(_ => Ok(Feed("server-v0.3.0-alpha", "server-v0.2.1-alpha")));

        var first = await checker.CheckAsync(manual: false, automaticChecksEnabled: true, includePreReleases: true, CancellationToken.None);
        var second = await checker.CheckAsync(manual: false, automaticChecksEnabled: true, includePreReleases: true, CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.Available, first.Outcome);
        Assert.Equal("0.3.0-alpha", first.Offer?.Latest.Version.ToString());
        Assert.True(first.Announce);
        Assert.False(second.Announce);
        Assert.Equal("0.3.0-alpha", store.Get().NotifiedVersion);
        Assert.NotNull(store.Get().LastCheckUtc);
    }

    [Fact]
    public async Task Nothing_newer_is_up_to_date()
    {
        var (checker, _, _) = Create(_ => Ok(Feed("server-v0.2.1-alpha", "server-v0.2.0-alpha")));

        var result = await checker.CheckAsync(false, true, true, CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.UpToDate, result.Outcome);
        Assert.Null(result.Offer);
    }

    [Fact]
    public async Task Pre_release_channel_off_hides_pre_releases()
    {
        var (checker, _, _) = Create(_ => Ok(Feed("server-v0.3.0-alpha")));

        var result = await checker.CheckAsync(false, true, includePreReleases: false, CancellationToken.None);

        Assert.Equal(UpdateCheckOutcome.UpToDate, result.Outcome);
    }

    [Fact]
    public async Task Second_check_sends_the_etag_and_reuses_the_cached_list_on_304()
    {
        var calls = 0;
        var (checker, _, handler) = Create(_ => ++calls == 1 ? Ok(Feed("server-v0.3.0-alpha")) : new HttpResponseMessage(HttpStatusCode.NotModified));

        await checker.CheckAsync(false, true, true, CancellationToken.None);
        var second = await checker.CheckAsync(manual: true, true, true, CancellationToken.None);

        Assert.Equal("\"e1\"", handler.Last!.Headers.IfNoneMatch.Single().ToString());
        Assert.Equal(UpdateCheckOutcome.Available, second.Outcome);
        Assert.Equal("0.3.0-alpha", second.Offer?.Latest.Version.ToString());
    }

    [Fact]
    public async Task Cache_survives_a_restart()
    {
        var (first, _, _) = Create(_ => Ok(Feed("server-v0.3.0-alpha")));
        await first.CheckAsync(false, true, true, CancellationToken.None);

        var (second, _, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.NotModified));
        var result = await second.CheckAsync(false, true, true, CancellationToken.None);

        Assert.Equal("\"e1\"", handler.Last!.Headers.IfNoneMatch.Single().ToString());
        Assert.Equal(UpdateCheckOutcome.Available, result.Outcome);
        Assert.False(result.Announce); // announced before the restart
    }

    [Fact]
    public async Task Manual_check_announces_even_when_already_announced_and_when_automatic_checks_are_off()
    {
        var (checker, _, _) = Create(_ => Ok(Feed("server-v0.3.0-alpha")));
        await checker.CheckAsync(false, true, true, CancellationToken.None);

        var result = await checker.CheckAsync(manual: true, automaticChecksEnabled: false, true, CancellationToken.None);

        Assert.True(result.Announce);
    }

    [Fact]
    public async Task Failures_are_reported_not_thrown()
    {
        var offline = Create(_ => throw new HttpRequestException("offline"));
        var limited = Create(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        var garbage = Create(_ => Ok("<html>not json</html>"));

        foreach (var (checker, store, _) in new[] { offline, limited, garbage })
        {
            var result = await checker.CheckAsync(false, true, true, CancellationToken.None);
            Assert.Equal(UpdateCheckOutcome.Failed, result.Outcome);
            Assert.NotNull(result.Error);
            Assert.Null(store.Get().LastCheckUtc);
        }
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        var (checker, _, _) = Create(_ => throw new TaskCanceledException());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => checker.CheckAsync(false, true, true, cts.Token));
    }
}
