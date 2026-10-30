using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public class ActionOutcomeTests
{
    [Fact]
    public void Factories_set_kind_code_and_message()
    {
        var failed = ActionOutcome.Failed(ActionFailureCode.NotFound, "no scene");
        Assert.Equal(ActionOutcomeKind.Failed, failed.Kind);
        Assert.Equal(ActionFailureCode.NotFound, failed.Code);
        Assert.Equal("no scene", failed.Message);

        var accepted = ActionOutcome.Accepted("sent");
        Assert.Equal(ActionOutcomeKind.Accepted, accepted.Kind);
        Assert.Null(accepted.Code);
        Assert.Equal("sent", accepted.Message);
    }

    [Fact]
    public void Success_is_one_shared_instance()
    {
        Assert.Same(ActionOutcome.Success, ActionOutcome.Success);
        Assert.Equal(ActionOutcomeKind.Success, ActionOutcome.Success.Kind);
    }

    [Fact]
    public void ThrowIfFailed_throws_only_for_a_failure()
    {
        ActionOutcome.Success.ThrowIfFailed();
        ActionOutcome.Accepted().ThrowIfFailed();
        var ex = Assert.Throws<InvalidOperationException>(() => ActionOutcome.Failed(ActionFailureCode.Timeout, "slow").ThrowIfFailed());
        Assert.Equal("slow", ex.Message);
        Assert.Equal("Timeout", Assert.Throws<InvalidOperationException>(() => ActionOutcome.Failed(ActionFailureCode.Timeout).ThrowIfFailed()).Message);
    }
}
