using System.Text.Json.Nodes;
using MacroGrid.Core.Automation;
using MacroGrid.Core.Model;

namespace MacroGrid.Tests;

public sealed class AutomationStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-auto-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static AutomationRule Variable(string id, string variable = "user.x") => new()
    {
        Id = id,
        Name = "Rule " + id,
        Trigger = new AutomationTrigger
        {
            Kind = AutomationTriggerKinds.Variable,
            Condition = new ConditionNode { Variable = variable, Operator = ">", Value = "1" },
        },
        Actions = [new ActionBinding("some.unknown.action", new JsonObject())],
    };

    private static AutomationRule Timed(string id, string time, params int[] days) => new()
    {
        Id = id,
        Trigger = new AutomationTrigger { Kind = AutomationTriggerKinds.Time, Time = time, Days = [.. days] },
    };

    [Fact]
    public void Rules_and_the_pause_survive_a_restart_and_an_unknown_action_type_is_accepted()
    {
        var store = new AutomationStore(_dir);
        Assert.True(store.TryReplace([Variable("a"), Timed("b", "07:30", 3, 1, 1)], true, out var error), error);

        var again = new AutomationStore(_dir);
        Assert.True(again.Paused);
        Assert.Equal(["a", "b"], again.List().Select(r => r.Id));
        Assert.Equal("some.unknown.action", again.List()[0].Actions[0].Type);
        Assert.Equal([1, 3], again.List()[1].Trigger.Days);
    }

    [Fact]
    public void A_replace_raises_Changed_and_a_refused_one_does_not_change_anything()
    {
        var store = new AutomationStore(_dir);
        var changes = 0;
        store.Changed += () => changes++;
        Assert.True(store.TryReplace([Variable("a")], false, out _));

        Assert.False(store.TryReplace([Variable("a"), Variable("a")], true, out var error));
        Assert.Contains("twice", error);
        Assert.Equal(1, changes);
        Assert.Equal(["a"], store.List().Select(r => r.Id));
        Assert.False(store.Paused);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("../x")]
    public void An_invalid_id_is_refused(string id)
    {
        Assert.False(new AutomationStore(_dir).TryReplace([Variable(id)], false, out _));
    }

    [Theory]
    [InlineData("7:30")]
    [InlineData("25:00")]
    [InlineData("07:60")]
    [InlineData("")]
    public void An_invalid_time_is_refused(string time)
    {
        Assert.False(new AutomationStore(_dir).TryReplace([Timed("t", time)], false, out _));
    }

    [Fact]
    public void Days_out_of_range_are_refused()
    {
        Assert.False(new AutomationStore(_dir).TryReplace([Timed("t", "07:30", 7)], false, out _));
    }

    [Fact]
    public void A_variable_rule_needs_a_readable_condition()
    {
        var store = new AutomationStore(_dir);
        var noCondition = Variable("a");
        noCondition.Trigger.Condition = null;
        Assert.False(store.TryReplace([noCondition], false, out _));

        var emptyAnd = Variable("b");
        emptyAnd.Trigger.Condition = new ConditionNode { Kind = ConditionKinds.And };
        Assert.False(store.TryReplace([emptyAnd], false, out _));

        var tooMany = Variable("c");
        tooMany.Trigger.Condition = new ConditionNode
        {
            Kind = ConditionKinds.Or,
            Children = [.. Enumerable.Range(0, AutomationLimits.MaxComparisons + 1).Select(i => new ConditionNode { Variable = "user.v" + i, Operator = ">", Value = "1" })],
        };
        Assert.False(store.TryReplace([tooMany], false, out _));
    }

    [Fact]
    public void The_limits_on_rules_steps_and_cooldown_are_enforced()
    {
        var store = new AutomationStore(_dir);
        Assert.False(store.TryReplace([.. Enumerable.Range(0, AutomationLimits.MaxRules + 1).Select(i => Variable("r" + i))], false, out _));

        var many = Variable("a");
        many.Actions = [.. Enumerable.Range(0, AutomationLimits.MaxSteps + 1).Select(_ => new ActionBinding("x", new JsonObject()))];
        Assert.False(store.TryReplace([many], false, out _));

        var slow = Variable("b");
        slow.CooldownSeconds = AutomationLimits.MaxCooldownSeconds + 1;
        Assert.False(store.TryReplace([slow], false, out _));
        Assert.Empty(store.List());
    }

    [Fact]
    public void Disable_switches_one_rule_off_and_saves_it()
    {
        var store = new AutomationStore(_dir);
        store.TryReplace([Variable("a"), Variable("b")], false, out _);

        Assert.True(store.Disable("a"));
        Assert.False(store.Disable("a"));
        Assert.False(store.Disable("missing"));

        var again = new AutomationStore(_dir);
        Assert.False(again.List().Single(r => r.Id == "a").Enabled);
        Assert.True(again.List().Single(r => r.Id == "b").Enabled);
    }

    [Fact]
    public void A_broken_file_is_moved_aside_and_the_list_is_empty()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "automation.json"), "{ not json");

        var store = new AutomationStore(_dir);

        Assert.Empty(store.List());
        Assert.True(File.Exists(Path.Combine(_dir, "automation.json.broken")));
    }

    [Fact]
    public void The_list_returned_is_a_copy()
    {
        var store = new AutomationStore(_dir);
        store.TryReplace([Variable("a")], false, out _);

        store.List()[0].Enabled = false;

        Assert.True(store.List()[0].Enabled);
    }
}
