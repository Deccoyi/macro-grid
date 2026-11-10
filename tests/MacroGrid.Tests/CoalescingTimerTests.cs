using MacroGrid.Core;

namespace MacroGrid.Tests;

public sealed class CoalescingTimerTests
{
    [Fact]
    public async Task Many_requests_in_one_window_run_the_callback_once_and_an_idle_timer_never_runs()
    {
        var runs = 0;
        using var timer = new CoalescingTimer(TimeSpan.FromMilliseconds(50), () => Interlocked.Increment(ref runs));

        await Task.Delay(150);
        Assert.Equal(0, runs);

        for (var i = 0; i < 20; i++) timer.Request();
        await Task.Delay(400);
        Assert.Equal(1, runs);

        timer.Request();
        await Task.Delay(400);
        Assert.Equal(2, runs);
    }

    [Fact]
    public async Task A_request_made_inside_the_callback_arms_the_next_run()
    {
        var runs = 0;
        CoalescingTimer? timer = null;
        timer = new CoalescingTimer(TimeSpan.FromMilliseconds(30), () => { if (Interlocked.Increment(ref runs) == 1) timer!.Request(); });
        using var _ = timer;
        timer.Request();
        await Task.Delay(500);
        Assert.Equal(2, runs);
    }
}
