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
        public bool ManualTimer;
        private readonly List<TaskCompletionSource> _timers = [];
        public WebViewSuspendController Controller;

        // Fake clock: no real time passes. Auto mode elapses the delay at once, manual mode waits for Fire().
        private Task Wait(TimeSpan _)
        {
            if (!ManualTimer) return Task.CompletedTask;
            var t = new TaskCompletionSource();
            _timers.Add(t);
            return t.Task;
        }

        public void Fire() { foreach (var t in _timers) t.SetResult(); _timers.Clear(); }

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
                TimeSpan.FromMilliseconds(20),
                Wait);
    }

    // Every continuation runs inline on the fake clock / gate, so assertions need no waiting.

    [Fact]
    public void Suspends_after_delay_and_resumes_when_shown()
    {
        var f = new Fixture { Want = true };
        f.Controller.Evaluate();
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
    public void Quick_restore_before_delay_never_suspends()
    {
        var f = new Fixture { Want = true, ManualTimer = true };
        f.Controller.Evaluate();
        f.Want = false;
        f.Controller.Evaluate();
        f.Fire();
        Assert.Equal(0, f.Suspends);
        Assert.Equal(0, f.Resumes);
    }

    [Fact]
    public void Failed_suspend_leaves_view_visible_and_not_suspended()
    {
        var f = new Fixture { Want = true, SuspendResult = false };
        f.Controller.Evaluate();
        Assert.False(f.Controller.IsSuspended);
        Assert.True(f.Visible);
        Assert.Equal(0, f.Resumes);
    }

    [Fact]
    public void Throwing_suspend_is_swallowed()
    {
        var f = new Fixture { Want = true, ThrowOnSuspend = true };
        f.Controller.Evaluate();
        Assert.False(f.Controller.IsSuspended);
        Assert.True(f.Visible);
    }

    [Fact]
    public void Restore_while_suspend_in_flight_resumes_afterwards()
    {
        var f = new Fixture { Want = true, Gate = new() };
        f.Controller.Evaluate();
        f.Want = false;
        f.Controller.Evaluate();
        f.Gate.SetResult(true);
        Assert.False(f.Controller.IsSuspended);
        Assert.Equal(1, f.Resumes);
        Assert.True(f.Visible);
    }

    [Fact]
    public void Repeated_evaluate_while_suspended_does_not_suspend_twice()
    {
        var f = new Fixture { Want = true };
        f.Controller.Evaluate();
        f.Controller.Evaluate();
        Assert.Equal(1, f.Suspends);
    }
}
