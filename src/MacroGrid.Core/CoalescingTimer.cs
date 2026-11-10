namespace MacroGrid.Core;

/// <summary>
/// A one-shot timer that is only armed while there is work. <see cref="Request"/> arms it if it is idle; the callback runs once, <c>delay</c> later, so every
/// request made in between is served by that one run. Idle, it holds no timer tick at all. A request made while the callback is still running arms it again.
/// </summary>
public sealed class CoalescingTimer : IDisposable
{
    private readonly Timer _timer;
    private readonly TimeSpan _delay;
    private int _armed;
    private volatile bool _disposed;

    public CoalescingTimer(TimeSpan delay, Action callback)
    {
        _delay = delay;
        _timer = new Timer(_ =>
        {
            Volatile.Write(ref _armed, 0); // before the callback, so a request made during it arms the next run
            if (!_disposed) callback();
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Request()
    {
        if (_disposed || Interlocked.Exchange(ref _armed, 1) == 1) return;
        try { _timer.Change(_delay, Timeout.InfiniteTimeSpan); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Dispose();
    }
}
