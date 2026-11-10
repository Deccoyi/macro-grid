using MacroGrid.Windows.Windows;

namespace MacroGrid.Tests;

public sealed class WebViewSuspendControllerTests
{
    private sealed class Fixture
    {
        public bool Want;
        public bool SuspendResult = true;
        public bool ThrowOnSuspend;
        public TaskCompletionSource<bool>? Gate;
        public int Suspends, Resumes;
        public bool Visible = true;
        public WebViewSuspendController Controller;

        public Fixture() =>
            Controller = new WebViewSuspendController(
                () => Want,
                async () =>
                {
                    Suspends++;
                    if (ThrowOnSuspend) throw new InvalidOperationException();
                    if (Gate is not null) await Gate.Task;
                    return SuspendResult;
                },
                () => Resumes++,
                v => Visible = v,
                TimeSpan.FromMilliseconds(20));
    }

    private static Task Settle() => Task.Delay(150);

    [Fact]
    public async Task Suspends_after_delay_and_resumes_when_shown()
    {
        var f = new Fixture { Want = true };
        f.Controller.Evaluate();
        await Settle();
        Assert.Equal(1, f.Suspends);
        Assert.True(f.Controller.IsSuspended);
        Assert.False(f.Visible);

        f.Want = false;
        f.Controller.Evaluate();
        Assert.Equal(1, f.Resumes);
        Assert.False(f.Controller.IsSuspended);
        Assert.True(f.Visible);
    }

    [Fact]
    public async Task Quick_restore_before_delay_never_suspends()
    {
        var f = new Fixture { Want = true };
        f.Controller.Evaluate();
        f.Want = false;
        f.Controller.Evaluate();
        await Settle();
        Assert.Equal(0, f.Suspends);
        Assert.Equal(0, f.Resumes);
    }

    [Fact]
    public async Task Failed_suspend_leaves_view_visible_and_not_suspended()
    {
        var f = new Fixture { Want = true, SuspendResult = false };
        f.Controller.Evaluate();
        await Settle();
        Assert.False(f.Controller.IsSuspended);
        Assert.True(f.Visible);
        Assert.Equal(0, f.Resumes);
    }

    [Fact]
    public async Task Throwing_suspend_is_swallowed()
    {
        var f = new Fixture { Want = true, ThrowOnSuspend = true };
        f.Controller.Evaluate();
        await Settle();
        Assert.False(f.Controller.IsSuspended);
        Assert.True(f.Visible);
    }

    [Fact]
    public async Task Restore_while_suspend_in_flight_resumes_afterwards()
    {
        var f = new Fixture { Want = true, Gate = new() };
        f.Controller.Evaluate();
        await Settle();
        f.Want = false;
        f.Controller.Evaluate();
        f.Gate.SetResult(true);
        await Settle();
        Assert.False(f.Controller.IsSuspended);
        Assert.Equal(1, f.Resumes);
        Assert.True(f.Visible);
    }

    [Fact]
    public async Task Repeated_evaluate_while_suspended_does_not_suspend_twice()
    {
        var f = new Fixture { Want = true };
        f.Controller.Evaluate();
        await Settle();
        f.Controller.Evaluate();
        await Settle();
        Assert.Equal(1, f.Suspends);
    }
}
