namespace MacroGrid.Windows.Windows;

/// <summary>
/// Decides when a window's WebView2 is suspended and resumed. UI-thread only; it holds no WebView2 types so the
/// logic is testable. A window that stays hidden or minimized for <c>delay</c> is hidden and suspended; any change
/// back to "wanted visible" resumes it at once. A failed or unavailable suspend is skipped, never retried in a loop,
/// and a resume failure is swallowed (the view then simply stays as the browser left it).
/// </summary>
public sealed class WebViewSuspendController(
    Func<bool> wantSuspended,
    Func<Task<bool>> trySuspend,
    Action resume,
    Action<bool> setVisible,
    TimeSpan delay)
{
    private int _generation;
    private bool _suspended;

    public bool IsSuspended => _suspended;

    /// <summary>Call on every window change that can alter the answer (state, visibility, enabled, session unlock).</summary>
    public void Evaluate()
    {
        var generation = ++_generation;
        if (wantSuspended()) _ = SuspendLaterAsync(generation);
        else Resume();
    }

    private async Task SuspendLaterAsync(int generation)
    {
        try
        {
            if (_suspended) return;
            await Task.Delay(delay);
            if (generation != _generation || !wantSuspended()) return;

            // The view must be invisible before TrySuspend is allowed.
            setVisible(false);
            var ok = await trySuspend();
            if (ok) _suspended = true;
            else setVisible(true);

            // The window came back while the suspend call was in flight.
            if (generation != _generation || !wantSuspended()) Resume();
        }
        catch (Exception)
        {
            // Unavailable or failed: leave the view running.
            try { setVisible(true); } catch (Exception) { /* window already gone */ }
        }
    }

    private void Resume()
    {
        try
        {
            setVisible(true);
            if (!_suspended) return;
            _suspended = false;
            resume();
        }
        catch (Exception)
        {
            // Window closing or WebView2 gone: nothing to bring back.
        }
    }
}
