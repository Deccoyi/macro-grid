using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>
/// The official catalog: two signed files (the index and the revoke list), each read from the repository contents API first and the project's
/// Pages site second, kept as a saved copy so nothing depends on the network at a given moment. There is no timer in here; the host's
/// background service and the API call <see cref="RefreshDueAsync"/> / the <c>Get…Async</c> methods when they want.
/// <para>
/// Rules that never bend: a file is trusted only after its signature verified with the official key; a file with a lower sequence than one this
/// PC accepted before is refused (an old valid file cannot be replayed); a source is only transport, so a refused answer simply moves on to the
/// next source; and nothing that fails to arrive, parse or verify can change what was verified earlier. See docs/design/plugin-distribution.md.
/// </para>
/// </summary>
public sealed class OfficialCatalog
{
    private const long MaxIndexEnvelopeBytes = 1536 * 1024;
    private const long MaxRevokedEnvelopeBytes = 256 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private sealed class Slot(string kind, string fileName, long maxBytes)
    {
        public string Kind { get; } = kind;
        public string FileName { get; } = fileName;
        public long MaxBytes { get; } = maxBytes;
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public DateTimeOffset? LastAttempt { get; set; }
    }

    private sealed record Fetched(bool NotModified, byte[]? Body, string? ETag, TimeSpan? RetryAfter, DateTimeOffset? ApiNotBefore);

    private readonly HttpClient _http;
    private readonly PluginCatalogStateStore _store;
    private readonly PluginCatalogClient _plainClient;
    private readonly TimeProvider _clock;
    private readonly byte[] _publicKey;
    private readonly Random _random;
    private readonly Slot _indexSlot = new(SignedCatalogFile.IndexKind, PluginSourceUrls.IndexFileName, MaxIndexEnvelopeBytes);
    private readonly Slot _revokedSlot = new(SignedCatalogFile.RevokedKind, PluginSourceUrls.RevokedFileName, MaxRevokedEnvelopeBytes);

    private volatile PluginCatalogIndex? _index;
    private volatile PluginRevocationList? _revoked;
    private volatile PluginCatalogIndex? _fallbackIndex;

    public OfficialCatalog(HttpClient http, PluginCatalogStateStore store, PluginCatalogClient plainClient, TimeProvider? clock = null, byte[]? publicKey = null, Random? random = null)
    {
        _http = http;
        _store = store;
        _plainClient = plainClient;
        _clock = clock ?? TimeProvider.System;
        _publicKey = publicKey ?? PluginSigning.OfficialPublicKey;
        _random = random ?? Random.Shared;

        _index = LoadSaved(_indexSlot, envelope => ParseIndex(envelope));
        _revoked = LoadSaved(_revokedSlot, envelope => ParseRevoked(envelope));
        if (_store.Get().CreatedUtc is null) _store.Update(s => s with { CreatedUtc = _clock.GetUtcNow() });
    }

    /// <summary>The verified index copy, or the plain index that was read while no signed one existed yet. No network.</summary>
    public PluginCatalogIndex? Index => _index ?? _fallbackIndex;

    /// <summary>The verified revoke list copy; null when there is none (then nothing is switched off). No network.</summary>
    public PluginRevocationList? Revoked => _revoked;

    /// <summary>The last successful check of the revoke list, or when this state was created if there never was one.</summary>
    public DateTimeOffset? RevokedCheckedOrCreatedUtc
    {
        get
        {
            var state = _store.Get();
            return state.Revoked.CheckedUtc ?? state.CreatedUtc;
        }
    }

    /// <summary>The copy when its last check is younger than <paramref name="maxAge"/>, otherwise one refresh; a failed refresh returns the copy.
    /// Throws only when there is no copy at all.</summary>
    public async Task<PluginCatalogIndex> GetIndexAsync(TimeSpan maxAge, CancellationToken cancellationToken, bool ignoreBackoff = false)
    {
        if (_index is { } copy && IsYoungerThan(_store.Get().Index, maxAge)) return copy;
        if (ignoreBackoff || BackoffOver(_store.Get().Index)) await RefreshAsync(_indexSlot, cancellationToken);
        if (_index is { } refreshed) return refreshed;

        // One-time fallback: a PC that never accepted a signed index reads the plain one, so Discover works before the first signed files are
        // published. After the first accepted signed index the plain file is never used for the official source again.
        if (_store.Get().Index.Sequence == 0)
        {
            var plain = await _plainClient.FetchIndexAsync(PluginSourceUrls.OfficialOwner, PluginSourceUrls.OfficialRepo, cancellationToken, fresh: ignoreBackoff);
            _fallbackIndex = plain;
            return plain;
        }
        throw new PluginCatalogException(PluginCatalogException.Fetch, "Could not reach the plugin catalog. Check your connection and try again.");
    }

    /// <summary>Same for the revoke list; never throws and returns null when there is no copy.</summary>
    public async Task<PluginRevocationList?> GetRevokedAsync(TimeSpan maxAge, CancellationToken cancellationToken, bool ignoreBackoff = false)
    {
        if (_revoked is { } copy && IsYoungerThan(_store.Get().Revoked, maxAge)) return copy;
        try
        {
            if (ignoreBackoff || BackoffOver(_store.Get().Revoked)) await RefreshAsync(_revokedSlot, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A failed look never switches anything off: what was verified earlier stays.
        }
        return _revoked;
    }

    /// <summary>For the background service: refreshes each file whose next check time has passed. True when a newer file was accepted.</summary>
    public async Task<bool> RefreshDueAsync(CancellationToken cancellationToken)
    {
        var now = _clock.GetUtcNow();
        var state = _store.Get();
        var any = false;
        if (state.Revoked.NextCheckUtc is not { } r || r <= now) any |= await RefreshAsync(_revokedSlot, cancellationToken);
        if (state.Index.NextCheckUtc is not { } i || i <= now) any |= await RefreshAsync(_indexSlot, cancellationToken);
        return any;
    }

    /// <summary>True when something is due now (used to decide whether the service needs to wake the network up at all).</summary>
    public bool AnyDue()
    {
        var now = _clock.GetUtcNow();
        var state = _store.Get();
        return state.Revoked.NextCheckUtc is not { } r || r <= now || state.Index.NextCheckUtc is not { } i || i <= now;
    }

    /// <summary>How long until the earliest file is due; zero when one is due now (or was never checked).</summary>
    public TimeSpan NextDueIn()
    {
        var now = _clock.GetUtcNow();
        var state = _store.Get();
        var next = new[] { state.Revoked.NextCheckUtc, state.Index.NextCheckUtc }.Select(t => t ?? now).Min();
        return next > now ? next - now : TimeSpan.Zero;
    }

    private bool IsYoungerThan(CatalogFileState file, TimeSpan maxAge) =>
        file.CheckedUtc is { } at && _clock.GetUtcNow() - at < maxAge;

    private bool BackoffOver(CatalogFileState file) => file.Failures == 0 || file.NextCheckUtc is not { } next || next <= _clock.GetUtcNow();

    private static CatalogFileState Of(PluginCatalogState state, Slot slot) => slot.Kind == SignedCatalogFile.IndexKind ? state.Index : state.Revoked;

    private static PluginCatalogState With(PluginCatalogState state, Slot slot, CatalogFileState file) =>
        slot.Kind == SignedCatalogFile.IndexKind ? state with { Index = file } : state with { Revoked = file };

    private T? LoadSaved<T>(Slot slot, Func<byte[], T?> parse) where T : class
    {
        var file = Of(_store.Get(), slot);
        if (file.Envelope is null) return null;
        var parsed = TryOpen(slot, Encoding.UTF8.GetBytes(file.Envelope), out _) is { } doc ? parse(doc.Payload) : null;
        if (parsed is null)
            _store.Update(s => With(s, slot, Of(s, slot) with { Envelope = null, ETag = null, ETagUrl = null }));
        return parsed;
    }

    private SignedCatalogDocument? TryOpen(Slot slot, byte[] envelope, out bool ok)
    {
        ok = SignedCatalogFile.TryRead(envelope, slot.Kind, _publicKey, out var document, slot.MaxBytes);
        return ok ? document : null;
    }

    private static PluginCatalogIndex? ParseIndex(byte[] payload)
    {
        try { return PluginCatalogClient.Parse(Encoding.UTF8.GetString(payload), PluginSourceUrls.OfficialOwner, PluginSourceUrls.OfficialRepo, signed: true); }
        catch (PluginCatalogException) { return null; }
    }

    private static PluginRevocationList? ParseRevoked(byte[] payload) => PluginRevocationList.Parse(payload);

    /// <returns>True when a newer file was accepted.</returns>
    private async Task<bool> RefreshAsync(Slot slot, CancellationToken cancellationToken)
    {
        await slot.Gate.WaitAsync(cancellationToken);
        try
        {
            var now = _clock.GetUtcNow();
            if (slot.LastAttempt is { } last && now - last < PluginCatalogPolicy.MinGap) return false;
            slot.LastAttempt = now;

            var hasCopy = slot.Kind == SignedCatalogFile.IndexKind ? _index is not null : _revoked is not null;
            var accepted = false;
            var succeeded = false;
            TimeSpan? retryAfter = null;

            foreach (var source in PluginSourceUrls.OfficialSources(slot.FileName))
            {
                if (source.IsApi && _store.Get().ApiNotBeforeUtc is { } notBefore && notBefore > now) continue;

                var file = Of(_store.Get(), slot);
                var etag = hasCopy && file.ETagUrl == source.Url.ToString() ? file.ETag : null;
                var fetched = await FetchAsync(source, etag, slot.MaxBytes, now, cancellationToken);
                if (fetched.ApiNotBefore is { } wait) _store.Update(s => s with { ApiNotBeforeUtc = wait });
                if (fetched.RetryAfter is { } ra && (retryAfter is null || ra > retryAfter)) retryAfter = ra;

                if (fetched.NotModified)
                {
                    succeeded = true;
                    break;
                }
                if (fetched.Body is not { } body) continue;

                if (TryOpen(slot, body, out _) is not { } document) continue;
                if (document.Sequence < file.Sequence) continue;

                if (document.Sequence == file.Sequence && hasCopy)
                {
                    // The same file again (a source without ETag support): nothing new.
                    _store.Update(s => With(s, slot, Of(s, slot) with { ETag = fetched.ETag, ETagUrl = source.Url.ToString() }));
                    succeeded = true;
                    break;
                }

                if (!Apply(slot, document)) continue;
                _store.Update(s => With(s, slot, Of(s, slot) with
                {
                    Envelope = Encoding.UTF8.GetString(body),
                    Sequence = document.Sequence,
                    ETag = fetched.ETag,
                    ETagUrl = source.Url.ToString(),
                }));
                accepted = true;
                succeeded = true;
                break;
            }

            var done = _clock.GetUtcNow();
            _store.Update(s =>
            {
                var file = Of(s, slot);
                return With(s, slot, succeeded
                    ? file with { CheckedUtc = done, Failures = 0, NextCheckUtc = PluginCatalogPolicy.NextAfterSuccess(slot.Kind, done, _random) }
                    : file with { Failures = file.Failures + 1, NextCheckUtc = PluginCatalogPolicy.NextAfterFailure(file.Failures + 1, done, retryAfter) });
            });
            return accepted;
        }
        finally
        {
            slot.Gate.Release();
        }
    }

    /// <summary>Parses a verified payload and swaps the in-memory copy. False when the payload is not something this server understands.</summary>
    private bool Apply(Slot slot, SignedCatalogDocument document)
    {
        if (slot.Kind == SignedCatalogFile.IndexKind)
        {
            if (ParseIndex(document.Payload) is not { } index) return false;
            _index = index;
            _fallbackIndex = null;
            return true;
        }
        if (ParseRevoked(document.Payload) is not { } list) return false;
        _revoked = list;
        return true;
    }

    private async Task<Fetched> FetchAsync(CatalogSource source, string? etag, long maxBytes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var failed = new Fetched(false, null, null, null, null);
        if (source.Url.Scheme != Uri.UriSchemeHttps) return failed;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
            if (source.IsApi) request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
            if (!string.IsNullOrEmpty(etag) && EntityTagHeaderValue.TryParse(etag, out var tag)) request.Headers.IfNoneMatch.Add(tag);

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

            var retryAfter = ReadRetryAfter(response, now);
            DateTimeOffset? apiNotBefore = source.IsApi ? ApiWait(response, now, retryAfter) : null;

            if (response.StatusCode == HttpStatusCode.NotModified) return new Fetched(true, null, etag, retryAfter, apiNotBefore);
            // A redirect is a failure: the files are fetched from fixed addresses only.
            if (response.StatusCode != HttpStatusCode.OK) return failed with { RetryAfter = retryAfter, ApiNotBefore = apiNotBefore };
            if (response.Content.Headers.ContentLength is { } declared && declared > maxBytes) return failed with { ApiNotBefore = apiNotBefore };

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var buffer = new MemoryStream();
            var chunk = new byte[16384];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > maxBytes) return failed with { ApiNotBefore = apiNotBefore };
            }
            return new Fetched(false, buffer.ToArray(), response.Headers.ETag?.ToString(), retryAfter, apiNotBefore);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return failed;
        }
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta) return delta;
        if (header?.Date is { } date) return date - now;
        return null;
    }

    /// <summary>The API's hourly quota: on 403/429 or with fewer than <see cref="PluginCatalogPolicy.ApiLowRemaining"/> calls left, the API is left
    /// alone until the reset time (or the Retry-After, whichever is later), at most 24 hours ahead.</summary>
    private static DateTimeOffset? ApiWait(HttpResponseMessage response, DateTimeOffset now, TimeSpan? retryAfter)
    {
        var limited = response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests;
        var remaining = HeaderLong(response, "X-RateLimit-Remaining");
        var low = remaining is { } r && r < PluginCatalogPolicy.ApiLowRemaining;
        if (!limited && !low) return null;

        var until = now;
        if (HeaderLong(response, "X-RateLimit-Reset") is { } reset) until = DateTimeOffset.FromUnixTimeSeconds(reset);
        if (retryAfter is { } ra && now + ra > until) until = now + ra;
        if (until <= now) until = now + TimeSpan.FromHours(1);
        var cap = now + PluginCatalogPolicy.ApiMaxWait;
        return until > cap ? cap : until;
    }

    private static long? HeaderLong(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) && long.TryParse(values.FirstOrDefault(), out var value) ? value : null;
}
