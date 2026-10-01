using System.Net;
using System.Net.Http.Headers;
using System.Text;
using MacroGrid.Core.Plugins.Distribution;

namespace MacroGrid.Tests;

public sealed class OfficialCatalogTests : IDisposable
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    /// <summary>Answers by host and file name and remembers every request (host, file, If-None-Match).</summary>
    private sealed class Net : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Api { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public Func<HttpRequestMessage, HttpResponseMessage> Pages { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public Func<HttpRequestMessage, HttpResponseMessage> Raw { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);
        public List<(string Host, string File, string? IfNoneMatch)> Requests { get; } = [];

        public int Count(string host) => Requests.Count(r => r.Host == host);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add((uri.Host, Path.GetFileName(uri.AbsolutePath), request.Headers.IfNoneMatch.FirstOrDefault()?.ToString()));
            var handler = uri.Host switch { "api.github.com" => Api, "deccoyi.github.io" => Pages, _ => Raw };
            return Task.FromResult(handler(request));
        }
    }

    private const string PlainIndex = """{ "formatVersion": 1, "name": "Plain", "plugins": [] }""";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-catalog-" + Guid.NewGuid().ToString("N"));
    private readonly Net _net = new();
    private readonly ManualTime _clock = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private PluginCatalogStateStore _store;
    private OfficialCatalog _catalog;
    private readonly HttpClient _http;

    public OfficialCatalogTests()
    {
        _http = new HttpClient(_net);
        _store = new PluginCatalogStateStore(_dir);
        _catalog = Build();
    }

    public void Dispose()
    {
        _http.Dispose();
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private OfficialCatalog Build() =>
        new(_http, _store, new PluginCatalogClient(_http, _clock), _clock, TestPluginSigning.PublicKey, new Random(1));

    private void Reload()
    {
        _store = new PluginCatalogStateStore(_dir);
        _catalog = Build();
    }

    private static byte[] Index(long sequence, string name = "Official") =>
        TestPluginSigning.SignEnvelope($$"""{ "kind": "index", "sequence": {{sequence}}, "formatVersion": 2, "name": "{{name}}", "plugins": [] }""");

    private static byte[] Revoked(long sequence, string id = "bad") =>
        TestPluginSigning.SignEnvelope($$"""{ "kind": "revoked", "sequence": {{sequence}}, "formatVersion": 1, "plugins": [ { "id": "{{id}}", "reason": "Unsafe." } ] }""");

    private static HttpResponseMessage Ok(byte[] body, string? etag = null, int? remaining = null, DateTimeOffset? reset = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        if (etag is not null) response.Headers.ETag = new EntityTagHeaderValue($"\"{etag}\"");
        if (remaining is { } r) response.Headers.Add("X-RateLimit-Remaining", r.ToString());
        if (reset is { } at) response.Headers.Add("X-RateLimit-Reset", at.ToUnixTimeSeconds().ToString());
        return response;
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    [Fact]
    public async Task The_first_fetch_stores_the_envelope_the_etag_and_the_sequence()
    {
        _net.Api = _ => Ok(Index(1), "e1");

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("Official", index.Name);
        var state = _store.Get().Index;
        Assert.Equal(1, state.Sequence);
        Assert.Equal("\"e1\"", state.ETag);
        Assert.NotNull(state.Envelope);
        Assert.Equal(_clock.GetUtcNow(), state.CheckedUtc);
        Assert.Equal(0, _net.Count("deccoyi.github.io"));
    }

    [Fact]
    public async Task A_call_inside_max_age_makes_no_request()
    {
        _net.Api = _ => Ok(Index(1), "e1");
        await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Single(_net.Requests);
    }

    [Fact]
    public async Task A_304_keeps_the_copy_and_renews_the_check_time()
    {
        _net.Api = r => r.Headers.IfNoneMatch.Count > 0 ? Status(HttpStatusCode.NotModified) : Ok(Index(1), "e1");
        await _catalog.GetIndexAsync(Hour, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("Official", index.Name);
        Assert.Equal("\"e1\"", _net.Requests[1].IfNoneMatch);
        Assert.Equal(_clock.GetUtcNow(), _store.Get().Index.CheckedUtc);
    }

    [Fact]
    public async Task An_etag_is_not_sent_to_the_other_source()
    {
        _net.Api = _ => Ok(Index(1), "e1");
        await _catalog.GetIndexAsync(Hour, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));
        _net.Api = _ => Status(HttpStatusCode.InternalServerError);
        _net.Pages = _ => Ok(Index(1), "p1");

        await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("\"e1\"", _net.Requests[1].IfNoneMatch);
        Assert.Null(_net.Requests[2].IfNoneMatch);
        Assert.Equal("deccoyi.github.io", _net.Requests[2].Host);
    }

    [Fact]
    public async Task When_the_api_fails_the_pages_site_is_used()
    {
        _net.Api = _ => Status(HttpStatusCode.ServiceUnavailable);
        _net.Pages = _ => Ok(Index(2, "FromPages"));

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("FromPages", index.Name);
        Assert.Equal(2, _store.Get().Index.Sequence);
    }

    [Fact]
    public async Task A_wrong_signature_from_the_api_falls_through_to_pages()
    {
        using var other = TestPluginSigning.NewOtherKey();
        _net.Api = _ => Ok(TestPluginSigning.SignEnvelope("""{ "kind": "index", "sequence": 9, "formatVersion": 2, "name": "Forged", "plugins": [] }""", other));
        _net.Pages = _ => Ok(Index(1));

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("Official", index.Name);
        Assert.Equal(1, _store.Get().Index.Sequence);
    }

    [Fact]
    public async Task A_lower_sequence_from_either_source_is_refused_and_the_copy_stays()
    {
        _net.Api = _ => Ok(Index(5, "Newest"));
        await _catalog.GetIndexAsync(Hour, CancellationToken.None);
        _clock.Advance(TimeSpan.FromHours(2));
        _net.Api = _ => Ok(Index(4, "Replay"));
        _net.Pages = _ => Ok(Index(3, "Older"));

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("Newest", index.Name);
        Assert.Equal(5, _store.Get().Index.Sequence);
        Assert.Equal(1, _store.Get().Index.Failures);
    }

    [Fact]
    public async Task A_quota_answer_sets_the_wait_and_the_next_refresh_skips_the_api()
    {
        var reset = _clock.GetUtcNow() + TimeSpan.FromMinutes(30);
        _net.Api = _ => { var r = Status(HttpStatusCode.Forbidden); r.Headers.Add("X-RateLimit-Remaining", "0"); r.Headers.Add("X-RateLimit-Reset", reset.ToUnixTimeSeconds().ToString()); return r; };
        _net.Pages = _ => Ok(Index(1));

        await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal(reset.ToUnixTimeSeconds(), _store.Get().ApiNotBeforeUtc!.Value.ToUnixTimeSeconds());
        Assert.Equal(1, _net.Count("deccoyi.github.io"));

        _clock.Advance(TimeSpan.FromMinutes(2));
        var apiBefore = _net.Count("api.github.com");

        await _catalog.GetIndexAsync(TimeSpan.Zero, CancellationToken.None, ignoreBackoff: true);

        Assert.Equal(apiBefore, _net.Count("api.github.com"));
    }

    [Fact]
    public async Task Retry_after_is_honoured_and_capped_at_a_day()
    {
        _net.Api = _ => { var r = Status(HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromHours(100)); return r; };
        _net.Pages = _ => Ok(Index(1));

        await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal(_clock.GetUtcNow() + TimeSpan.FromHours(24), _store.Get().ApiNotBeforeUtc);
    }

    [Fact]
    public async Task Few_calls_remaining_on_a_good_answer_also_sets_the_wait()
    {
        var reset = _clock.GetUtcNow() + TimeSpan.FromMinutes(40);
        _net.Api = _ => Ok(Index(1), "e1", remaining: 5, reset: reset);

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("Official", index.Name);
        Assert.Equal(reset.ToUnixTimeSeconds(), _store.Get().ApiNotBeforeUtc!.Value.ToUnixTimeSeconds());
    }

    [Fact]
    public async Task When_every_source_fails_the_copy_stays_and_the_wait_doubles()
    {
        _net.Api = _ => Ok(Revoked(1));
        await _catalog.GetRevokedAsync(Hour, CancellationToken.None);
        _net.Api = _ => Status(HttpStatusCode.InternalServerError);
        _net.Pages = _ => Status(HttpStatusCode.InternalServerError);

        _clock.Advance(TimeSpan.FromHours(10));
        var first = await _catalog.GetRevokedAsync(Hour, CancellationToken.None);
        var afterFirst = _store.Get().Revoked;
        Assert.Equal(TimeSpan.FromMinutes(30), afterFirst.NextCheckUtc!.Value - _clock.GetUtcNow());
        _clock.Advance(TimeSpan.FromMinutes(30));
        await _catalog.GetRevokedAsync(Hour, CancellationToken.None);
        var afterSecond = _store.Get().Revoked;

        Assert.NotNull(first!.Find("bad", "1.0"));
        Assert.Equal(1, afterFirst.Failures);
        Assert.Equal(2, afterSecond.Failures);
        Assert.Equal(TimeSpan.FromMinutes(60), afterSecond.NextCheckUtc!.Value - _clock.GetUtcNow());
    }

    [Fact]
    public async Task A_redirect_answer_is_a_failure()
    {
        _net.Api = _ => { var r = Status(HttpStatusCode.Found); r.Headers.Location = new Uri("https://evil.example.com/x"); return r; };
        _net.Pages = _ => Ok(Index(1));

        var index = await _catalog.GetIndexAsync(Hour, CancellationToken.None);

        Assert.Equal("Official", index.Name);
        Assert.DoesNotContain(_net.Requests, r => r.Host == "evil.example.com");
    }

    [Fact]
    public async Task An_oversized_body_is_a_failure_and_no_list_means_no_list()
    {
        _net.Api = _ => Ok(new byte[300 * 1024]);

        var list = await _catalog.GetRevokedAsync(Hour, CancellationToken.None);

        Assert.Null(list);
        Assert.Null(_catalog.Revoked);
        Assert.Equal(1, _store.Get().Revoked.Failures);
    }

    [Fact]
    public async Task A_tampered_state_file_drops_the_envelope_and_keeps_the_sequence()
    {
        _net.Api = _ => Ok(Index(3));
        await _catalog.GetIndexAsync(Hour, CancellationToken.None);
        var path = Path.Combine(_dir, "plugin-catalog-state.json");
        var text = File.ReadAllText(path);
        var start = text.IndexOf("\"envelope\": \"", StringComparison.Ordinal) + 13;
        File.WriteAllText(path, text[..start] + "x" + text[(start + 1)..]);

        Reload();

        Assert.Null(_catalog.Index);
        Assert.Equal(3, _store.Get().Index.Sequence);
        Assert.Null(_store.Get().Index.Envelope);

        // an older file is still refused; the same sequence restores the copy
        _net.Api = _ => Ok(Index(2, "Old"));
        _net.Pages = _ => Status(HttpStatusCode.NotFound);
        await Assert.ThrowsAsync<PluginCatalogException>(() => _catalog.GetIndexAsync(Hour, CancellationToken.None));
        _clock.Advance(TimeSpan.FromHours(2));
        _net.Api = _ => Ok(Index(3));
        Assert.Equal("Official", (await _catalog.GetIndexAsync(Hour, CancellationToken.None, ignoreBackoff: true)).Name);
    }

    [Fact]
    public async Task The_plain_index_is_a_one_time_fallback_before_any_signed_index()
    {
        _net.Raw = _ => Ok(Encoding.UTF8.GetBytes(PlainIndex));

        var plain = await _catalog.GetIndexAsync(Hour, CancellationToken.None);
        Assert.Equal("Plain", plain.Name);
        Assert.Equal(0, _store.Get().Index.Sequence);

        _clock.Advance(TimeSpan.FromHours(2));
        _net.Api = _ => Ok(Index(1));
        Assert.Equal("Official", (await _catalog.GetIndexAsync(Hour, CancellationToken.None, ignoreBackoff: true)).Name);

        // After a signed index was accepted the plain file is never used again, even when the copy is lost.
        var raw = _net.Count("raw.githubusercontent.com");
        var path = Path.Combine(_dir, "plugin-catalog-state.json");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"envelope\": \"", "\"envelope\": \"x"));
        Reload();
        _net.Api = _ => Status(HttpStatusCode.NotFound);

        await Assert.ThrowsAsync<PluginCatalogException>(() => _catalog.GetIndexAsync(Hour, CancellationToken.None, ignoreBackoff: true));
        Assert.Equal(raw, _net.Count("raw.githubusercontent.com"));
    }

    [Fact]
    public async Task Two_refreshes_within_a_minute_make_one_request()
    {
        _net.Api = _ => Status(HttpStatusCode.InternalServerError);
        _net.Pages = _ => Status(HttpStatusCode.InternalServerError);

        await _catalog.GetRevokedAsync(TimeSpan.Zero, CancellationToken.None, ignoreBackoff: true);
        await _catalog.GetRevokedAsync(TimeSpan.Zero, CancellationToken.None, ignoreBackoff: true);

        Assert.Equal(1, _net.Count("api.github.com"));
    }

    [Fact]
    public async Task The_background_refresh_only_touches_files_that_are_due()
    {
        _net.Api = r => r.RequestUri!.AbsolutePath.EndsWith("revoked.signed.json") ? Ok(Revoked(1)) : Ok(Index(1));

        Assert.True(await _catalog.RefreshDueAsync(CancellationToken.None));
        var calls = _net.Requests.Count;
        Assert.False(await _catalog.RefreshDueAsync(CancellationToken.None));
        Assert.Equal(calls, _net.Requests.Count);

        _clock.Advance(TimeSpan.FromHours(10));
        await _catalog.RefreshDueAsync(CancellationToken.None);
        Assert.Contains(_net.Requests.Skip(calls), r => r.File == "revoked.signed.json");
        Assert.DoesNotContain(_net.Requests.Skip(calls), r => r.File == "index.signed.json");
    }
}

public sealed class PluginCatalogStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-catalog-state-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public void State_round_trips()
    {
        var first = new PluginCatalogStateStore(_dir);
        first.Update(s => s with { Index = new CatalogFileState { Sequence = 7, ETag = "\"a\"", Failures = 2 }, ApiNotBeforeUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });

        var second = new PluginCatalogStateStore(_dir).Get();

        Assert.Equal(7, second.Index.Sequence);
        Assert.Equal("\"a\"", second.Index.ETag);
        Assert.Equal(2, second.Index.Failures);
        Assert.NotNull(second.ApiNotBeforeUtc);
    }

    [Fact]
    public void An_unreadable_file_is_moved_aside_and_the_state_starts_empty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "plugin-catalog-state.json"), "{ nope");

        var store = new PluginCatalogStateStore(_dir);

        Assert.Equal(0, store.Get().Index.Sequence);
        Assert.True(File.Exists(Path.Combine(_dir, "plugin-catalog-state.json.broken")));
    }
}

public sealed class PluginCatalogClientCopyTests
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    [Fact]
    public async Task An_index_is_kept_for_five_minutes_unless_a_fresh_one_is_asked_for()
    {
        var clock = new ManualTime(DateTimeOffset.UtcNow);
        var handler = new FakeHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{ "formatVersion": 1, "name": "N", "plugins": [] }""") });
        var client = new PluginCatalogClient(new HttpClient(handler), clock);

        await client.FetchIndexAsync("a", "b", CancellationToken.None);
        await client.FetchIndexAsync("a", "b", CancellationToken.None);
        Assert.Equal(1, handler.Calls);

        await client.FetchIndexAsync("a", "b", CancellationToken.None, fresh: true);
        Assert.Equal(2, handler.Calls);

        clock.Advance(TimeSpan.FromMinutes(6));
        await client.FetchIndexAsync("a", "b", CancellationToken.None);
        Assert.Equal(3, handler.Calls);
    }
}

public sealed class PluginCatalogPolicyTests
{
    [Fact]
    public void The_next_check_falls_inside_the_stated_ranges()
    {
        var now = DateTimeOffset.UnixEpoch;
        var random = new Random(7);
        for (var i = 0; i < 200; i++)
        {
            var revoked = PluginCatalogPolicy.NextAfterSuccess("revoked", now, random) - now;
            Assert.InRange(revoked, TimeSpan.FromHours(8), TimeSpan.FromHours(9));
            var index = PluginCatalogPolicy.NextAfterSuccess("index", now, random) - now;
            Assert.InRange(index, TimeSpan.FromHours(24), TimeSpan.FromHours(26));
        }
    }

    [Fact]
    public void A_failure_waits_30_minutes_then_doubles_up_to_a_day_and_honours_retry_after()
    {
        var now = DateTimeOffset.UnixEpoch;

        Assert.Equal(TimeSpan.FromMinutes(30), PluginCatalogPolicy.NextAfterFailure(1, now, null) - now);
        Assert.Equal(TimeSpan.FromMinutes(60), PluginCatalogPolicy.NextAfterFailure(2, now, null) - now);
        Assert.Equal(TimeSpan.FromHours(24), PluginCatalogPolicy.NextAfterFailure(30, now, null) - now);
        Assert.Equal(TimeSpan.FromHours(5), PluginCatalogPolicy.NextAfterFailure(1, now, TimeSpan.FromHours(5)) - now);
        Assert.Equal(TimeSpan.FromHours(24), PluginCatalogPolicy.NextAfterFailure(1, now, TimeSpan.FromDays(9)) - now);
    }
}
