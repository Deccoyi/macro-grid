using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public class ActionFlowTests
{
    public sealed record FlowCase(string Name, List<string> Steps, List<bool> Met, List<int> Run, List<int> Depths);

    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shared", "action-flow-cases.json");
        var cases = JsonSerializer.Deserialize<List<FlowCase>>(File.ReadAllText(path), ProtocolJson.Options)!;
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Shared_table(FlowCase c)
    {
        var flow = new ActionFlow.Run();
        var met = 0;
        var ran = new List<int>();
        for (var i = 0; i < c.Steps.Count; i++)
        {
            var step = flow.Next(c.Steps[i], () => c.Met[met++]);
            if (step == FlowStep.Stop) break;
            if (step == FlowStep.Run) ran.Add(i);
        }
        Assert.Equal(c.Run, ran);
        Assert.Equal(c.Depths, ActionFlow.Depths(c.Steps));
    }

    // ---- through the dispatcher ----

    private sealed class Recorder(string type, bool fails = false, Action? onRun = null) : IActionHandler
    {
        public string Type => type;
        public string DisplayName => type;
        public int Runs { get; private set; }

        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
        {
            Runs++;
            onRun?.Invoke();
            return fails ? throw new InvalidOperationException("boom") : Task.CompletedTask;
        }
    }

    private static ActionBinding B(string type, JsonObject? settings = null) => new(type, settings ?? []);

    private static JsonObject Compare(string variable, string value) =>
        new() { ["when"] = "condition", ["condition"] = new JsonObject { ["kind"] = "compare", ["variable"] = variable, ["operator"] = "==", ["value"] = value } };

    private static async Task<IReadOnlyList<ActionFailure>> Press(ActionDispatcher dispatcher, params ActionBinding[] steps)
    {
        var widget = new Widget { Id = "w", Actions = new() { [WidgetEvents.Press] = [.. steps] } };
        return await dispatcher.DispatchAsync(widget, WidgetEvents.Press, new ActionContext("dev", "page", "w", new FakeDeviceController()), CancellationToken.None);
    }

    private static ActionDispatcher Make(VariableStore? store, params IActionHandler[] handlers) =>
        new([.. handlers, new IfAction(), new ElseAction(), new EndIfAction(), new StopAction()], NullLogger<ActionDispatcher>.Instance, store);

    [Fact]
    public async Task The_then_branch_and_the_otherwise_branch_follow_the_condition()
    {
        var store = new VariableStore();
        var a = new Recorder("t.a");
        var b = new Recorder("t.b");
        var dispatcher = Make(store, a, b);
        var steps = new[] { B("core.if", Compare("test.on", "true")), B("t.a"), B("core.else"), B("t.b"), B("core.endIf") };

        store.Set("test.on", true);
        await Press(dispatcher, steps);
        Assert.Equal((1, 0), (a.Runs, b.Runs));

        store.Set("test.on", false);
        await Press(dispatcher, steps);
        Assert.Equal((1, 1), (a.Runs, b.Runs));
    }

    [Fact]
    public async Task Previous_failed_and_previous_ok_look_at_the_last_step_that_ran()
    {
        var failing = new Recorder("t.fail", fails: true);
        var after = new Recorder("t.after");
        var ok = new Recorder("t.ok");
        var dispatcher = Make(new VariableStore(), failing, after, ok);

        var errors = await Press(dispatcher, B("t.fail"), B("core.if", new() { ["when"] = "previousFailed" }), B("t.after"), B("core.endIf"),
            B("core.if", new() { ["when"] = "previousOk" }), B("t.ok"), B("core.endIf"));

        Assert.Equal(1, after.Runs);
        Assert.Equal(1, ok.Runs); // the step inside the first block succeeded, so the step before the second If did not fail
        Assert.Single(errors); // a failure is still reported when an If reacts to it
    }

    [Fact]
    public async Task Nothing_has_failed_before_any_step_ran()
    {
        var a = new Recorder("t.a");
        var dispatcher = Make(new VariableStore(), a);
        await Press(dispatcher, B("core.if", new() { ["when"] = "previousFailed" }), B("t.a"), B("core.endIf"));
        Assert.Equal(0, a.Runs);
    }

    [Fact]
    public async Task A_skipped_step_does_not_change_previous_failed()
    {
        var failing = new Recorder("t.fail", fails: true);
        var a = new Recorder("t.a");
        var dispatcher = Make(new VariableStore(), failing, a);
        await Press(dispatcher, B("t.fail"), B("core.if", Compare("test.never", "1")), B("t.a"), B("core.endIf"),
            B("core.if", new() { ["when"] = "previousFailed" }), B("t.a"), B("core.endIf"));
        Assert.Equal(1, a.Runs);
    }

    [Fact]
    public async Task Stop_ends_the_list_without_a_failure()
    {
        var a = new Recorder("t.a");
        var b = new Recorder("t.b");
        var dispatcher = Make(new VariableStore(), a, b);
        var errors = await Press(dispatcher, B("t.a"), B("core.stop"), B("t.b"));
        Assert.Equal((1, 0), (a.Runs, b.Runs));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_condition_is_read_when_the_if_is_reached()
    {
        var store = new VariableStore();
        var setter = new Recorder("t.set", onRun: () => store.Set("test.on", true));
        var a = new Recorder("t.a");
        var dispatcher = Make(store, setter, a);
        await Press(dispatcher, B("t.set"), B("core.if", Compare("test.on", "true")), B("t.a"), B("core.endIf"));
        Assert.Equal(1, a.Runs);
    }

    [Fact]
    public async Task A_missing_or_unreadable_condition_counts_as_not_met()
    {
        var a = new Recorder("t.a");
        var dispatcher = Make(new VariableStore(), a);
        await Press(dispatcher, B("core.if"), B("t.a"), B("core.endIf"));
        await Press(dispatcher, B("core.if", new() { ["condition"] = new JsonObject { ["kind"] = 5 } }), B("t.a"), B("core.endIf"));
        await Press(Make(null, a), B("core.if", Compare("test.on", "true")), B("t.a"), B("core.endIf"));
        Assert.Equal(0, a.Runs);
    }

    [Fact]
    public async Task A_list_that_is_not_well_formed_still_ends()
    {
        var a = new Recorder("t.a");
        var dispatcher = Make(new VariableStore(), a);
        await Press(dispatcher, B("core.endIf"), B("core.else"), B("t.a"), B("core.if", Compare("test.never", "1")), B("t.a"));
        Assert.Equal(1, a.Runs);
    }

    [Fact]
    public async Task The_logic_steps_are_known_actions_so_they_are_not_reported_as_missing()
    {
        var dispatcher = Make(new VariableStore());
        Assert.Empty(await Press(dispatcher, B("core.if", new() { ["when"] = "previousOk" }), B("core.else"), B("core.endIf"), B("core.stop")));
        Assert.All(new IActionHandler[] { new IfAction(), new ElseAction(), new EndIfAction(), new StopAction(), new DelayAction() },
            h => Assert.Equal("Logic", ((IActionDescriptor)h).Category));
    }
}
