namespace MacroGrid.Core.Sessions;

/// <summary>A token bucket: <paramref name="Capacity"/> bursts, refilled at <paramref name="PerSecond"/>.</summary>
internal sealed class TokenBucket
{
    private readonly double _capacity;
    private readonly double _perSecond;
    private double _tokens;
    private DateTimeOffset _at;
    private bool _started;

    public TokenBucket(double capacity, double perSecond)
    {
        _capacity = capacity;
        _perSecond = perSecond;
        _tokens = capacity;
    }

    public bool Take(DateTimeOffset now)
    {
        if (!_started) { _started = true; _at = now; }
        _tokens = Math.Min(_capacity, _tokens + (now - _at).TotalSeconds * _perSecond);
        _at = now;
        if (_tokens < 1) return false;
        _tokens -= 1;
        return true;
    }
}
