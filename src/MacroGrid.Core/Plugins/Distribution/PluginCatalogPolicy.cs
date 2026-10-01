namespace MacroGrid.Core.Plugins.Distribution;

/// <summary>The numbers that decide how often the official catalog is looked at. They sit together so the schedule can be read in one place
/// (the same idea as <c>UpdatePolicy</c>); a random part is added so many computers do not call at the same minute.</summary>
public static class PluginCatalogPolicy
{
    public static readonly TimeSpan RevokedInterval = TimeSpan.FromHours(8);
    public static readonly TimeSpan RevokedJitter = TimeSpan.FromMinutes(60);
    public static readonly TimeSpan IndexInterval = TimeSpan.FromHours(24);
    public static readonly TimeSpan IndexJitter = TimeSpan.FromMinutes(120);

    /// <summary>The first look after start: this plus up to <see cref="StartJitter"/>.</summary>
    public static readonly TimeSpan StartDelay = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan StartJitter = TimeSpan.FromMinutes(10);

    /// <summary>No successful check of the revoke list for this long: one quiet warning line (nothing is switched off because of it).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(14);

    public static readonly TimeSpan BackoffBase = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan BackoffMax = TimeSpan.FromHours(24);

    /// <summary>Two refreshes of the same file are never closer than this, whoever asks.</summary>
    public static readonly TimeSpan MinGap = TimeSpan.FromSeconds(60);

    /// <summary>The API is left alone when this few calls of its hourly quota remain.</summary>
    public const int ApiLowRemaining = 10;
    public static readonly TimeSpan ApiMaxWait = TimeSpan.FromHours(24);

    public static DateTimeOffset NextAfterSuccess(string kind, DateTimeOffset now, Random random)
    {
        var (interval, jitter) = kind == SignedCatalogFile.RevokedKind ? (RevokedInterval, RevokedJitter) : (IndexInterval, IndexJitter);
        return now + interval + TimeSpan.FromMinutes(random.NextDouble() * jitter.TotalMinutes);
    }

    /// <summary>30 minutes, doubling with every failure in a row, at most 24 hours; later when a source said so with <c>Retry-After</c>.</summary>
    public static DateTimeOffset NextAfterFailure(int failures, DateTimeOffset now, TimeSpan? retryAfter)
    {
        var steps = Math.Clamp(failures - 1, 0, 12);
        var wait = TimeSpan.FromTicks(Math.Min(BackoffBase.Ticks << steps, BackoffMax.Ticks));
        if (retryAfter is { } asked && asked > wait) wait = asked;
        return now + (wait > BackoffMax ? BackoffMax : wait);
    }
}
