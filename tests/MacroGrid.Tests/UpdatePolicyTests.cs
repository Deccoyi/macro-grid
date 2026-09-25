using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class UpdatePolicyTests
{
    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private static readonly ReleaseVersion V030 = new(0, 3, 0, "alpha");
    private static readonly ReleaseVersion V031 = new(0, 3, 1, "alpha");

    private readonly TestClock _clock = new(new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero));

    private UpdatePolicy Policy() => new(_clock);

    [Fact]
    public void Announces_a_new_version_once()
    {
        var policy = Policy();
        var state = new UpdateState();

        Assert.True(policy.ShouldNotify(state, V030, automaticChecksEnabled: true, manual: false));

        state = policy.MarkNotified(state, V030);
        Assert.False(policy.ShouldNotify(state, V030, true, false));
        Assert.True(policy.ShouldNotify(state, V031, true, false));
    }

    [Fact]
    public void Switched_off_stays_quiet_but_a_manual_check_still_announces()
    {
        var policy = Policy();

        Assert.False(policy.ShouldNotify(new UpdateState(), V030, automaticChecksEnabled: false, manual: false));
        Assert.True(policy.ShouldNotify(new UpdateState(), V030, automaticChecksEnabled: false, manual: true));
    }

    [Fact]
    public void Later_is_quiet_for_24_hours_then_announces_again()
    {
        var policy = Policy();
        var state = policy.Snooze(policy.MarkNotified(new UpdateState(), V030));

        Assert.False(policy.ShouldNotify(state, V030, true, false));
        _clock.Advance(TimeSpan.FromHours(23));
        Assert.False(policy.ShouldNotify(state, V030, true, false));
        _clock.Advance(TimeSpan.FromHours(1));
        Assert.True(policy.ShouldNotify(state, V030, true, false));
    }

    [Fact]
    public void Skip_silences_that_version_only()
    {
        var state = UpdatePolicy.Skip(new UpdateState(), V030);

        Assert.True(UpdatePolicy.IsSkipped(state, V030));
        Assert.False(Policy().ShouldNotify(state, V030, true, false));
        Assert.True(Policy().ShouldNotify(state, V031, true, false));
    }

    [Fact]
    public void Manual_check_ignores_snooze_and_skip()
    {
        var policy = Policy();
        var state = UpdatePolicy.Skip(policy.Snooze(new UpdateState()), V030);

        Assert.True(policy.ShouldNotify(state, V030, true, manual: true));
    }
}
