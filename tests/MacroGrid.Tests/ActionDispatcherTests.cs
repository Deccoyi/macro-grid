using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

/// <summary>A press handler that also implements IReleaseAwareAction ("play while held"), recording every call.</summary>
public sealed class ReleaseAwareHandler : IActionHandler, IReleaseAwareAction
{
    public string Type => "test.releaseAware";
    public string DisplayName => "Release aware";
    public List<string> Calls { get; } = [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        Calls.Add("execute");
        return Task.CompletedTask;
    }

    public Task ReleaseAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        Calls.Add("release");
        return Task.CompletedTask;
    }
}

public sealed class PlainHandler : IActionHandler
{
    public string Type => "test.plain";
    public string DisplayName => "Plain";
    public List<string> Calls { get; } = [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        Calls.Add("execute");
        return Task.CompletedTask;
    }
}

public class ActionDispatcherTests
{
    private static ActionContext Context() => new("dev", "page", "widget", new FakeDeviceController());

    private static Widget WidgetWithPress(string type) => new()
    {
        Actions = { [WidgetEvents.Press] = [new ActionBinding(type, [])] },
    };

    [Fact]
    public async Task Release_event_calls_ReleaseAsync_on_the_press_binding_first()
    {
        var handler = new ReleaseAwareHandler();
        var dispatcher = new ActionDispatcher([handler], NullLogger<ActionDispatcher>.Instance);
        var widget = WidgetWithPress(handler.Type);

        await dispatcher.DispatchAsync(widget, WidgetEvents.Press, Context(), default);
        await dispatcher.DispatchAsync(widget, WidgetEvents.Release, Context(), default);

        Assert.Equal(["execute", "release"], handler.Calls);
    }

    [Fact]
    public async Task Release_event_ignores_a_press_binding_that_is_not_release_aware()
    {
        var handler = new PlainHandler();
        var dispatcher = new ActionDispatcher([handler], NullLogger<ActionDispatcher>.Instance);
        var widget = WidgetWithPress(handler.Type);

        var errors = await dispatcher.DispatchAsync(widget, WidgetEvents.Release, Context(), default);

        Assert.Empty(errors);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task Release_event_still_runs_its_own_bindings_after_the_release_hook()
    {
        var pressHandler = new ReleaseAwareHandler();
        var releaseHandler = new PlainHandler();
        var dispatcher = new ActionDispatcher([pressHandler, releaseHandler], NullLogger<ActionDispatcher>.Instance);
        var widget = WidgetWithPress(pressHandler.Type);
        widget.Actions[WidgetEvents.Release] = [new ActionBinding(releaseHandler.Type, [])];

        await dispatcher.DispatchAsync(widget, WidgetEvents.Release, Context(), default);

        Assert.Equal(["release"], pressHandler.Calls);
        Assert.Equal(["execute"], releaseHandler.Calls);
    }

    [Fact]
    public async Task A_failing_ReleaseAsync_is_collected_and_does_not_stop_the_release_bindings()
    {
        var pressHandler = new ThrowingReleaseHandler();
        var releaseHandler = new PlainHandler();
        var dispatcher = new ActionDispatcher([pressHandler, releaseHandler], NullLogger<ActionDispatcher>.Instance);
        var widget = WidgetWithPress(pressHandler.Type);
        widget.Actions[WidgetEvents.Release] = [new ActionBinding(releaseHandler.Type, [])];

        var errors = await dispatcher.DispatchAsync(widget, WidgetEvents.Release, Context(), default);

        Assert.Equal(["release failed"], errors.Select(e => e.Message));
        Assert.Equal(["execute"], releaseHandler.Calls);
    }

private sealed class OutcomeHandler(Func<Task<ActionOutcome>> run) : IActionHandler, IActionOutcomeHandler
    {
        public string Type => "test.outcome";
        public string DisplayName => "Outcome";
        public int ExecuteCalls { get; private set; }

        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
        {
            ExecuteCalls++;
            return Task.CompletedTask;
        }

        public Task<ActionOutcome> ExecuteWithOutcomeAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => run();
    }

    private sealed class ThrowingHandler(string message) : IActionHandler
    {
        public string Type => "test.throwing";
        public string DisplayName => "Throwing";
        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => throw new InvalidOperationException(message);
    }

    private static async Task<IReadOnlyList<ActionFailure>> Press(params IActionHandler[] handlers)
    {
        var dispatcher = new ActionDispatcher(handlers, NullLogger<ActionDispatcher>.Instance);
        var widget = new Widget { Actions = { [WidgetEvents.Press] = [.. handlers.Select(h => new ActionBinding(h.Type, []))] } };
        return await dispatcher.DispatchAsync(widget, WidgetEvents.Press, Context(), default);
    }

    [Fact]
    public async Task A_legacy_handler_that_throws_is_a_provider_error_with_its_text()
    {
        var failures = await Press(new ThrowingHandler("boom"));

        var failure = Assert.Single(failures);
        Assert.Equal(ActionFailureCode.ProviderError, failure.Code);
        Assert.Equal("boom", failure.Message);
        Assert.Equal("test.throwing", failure.ActionType);
    }

    [Fact]
    public async Task Success_and_accepted_outcomes_are_no_failure()
    {
        Assert.Empty(await Press(new OutcomeHandler(() => Task.FromResult(ActionOutcome.Success))));
        Assert.Empty(await Press(new OutcomeHandler(() => Task.FromResult(ActionOutcome.Accepted("sent")))));
    }

    [Fact]
    public async Task A_failed_outcome_keeps_its_code_and_text()
    {
        var failures = await Press(new OutcomeHandler(() => Task.FromResult(ActionOutcome.Failed(ActionFailureCode.NotConnected, "x"))));

        var failure = Assert.Single(failures);
        Assert.Equal(ActionFailureCode.NotConnected, failure.Code);
        Assert.Equal("x", failure.Message);
    }

    [Fact]
    public async Task A_failed_outcome_without_text_gets_the_default_for_its_code()
    {
        var failures = await Press(new OutcomeHandler(() => Task.FromResult(ActionOutcome.Failed(ActionFailureCode.NotConfigured))));

        Assert.Equal("This action is not set up yet.", Assert.Single(failures).Message);
    }

    [Fact]
    public async Task A_null_outcome_and_a_throwing_outcome_are_provider_errors()
    {
        var nothing = Assert.Single(await Press(new OutcomeHandler(() => Task.FromResult<ActionOutcome>(null!))));
        Assert.Equal(ActionFailureCode.ProviderError, nothing.Code);

        var thrown = Assert.Single(await Press(new OutcomeHandler(() => throw new InvalidOperationException("bad"))));
        Assert.Equal(ActionFailureCode.ProviderError, thrown.Code);
        Assert.Equal("bad", thrown.Message);
    }

    [Fact]
    public async Task The_dispatcher_never_calls_ExecuteAsync_of_an_outcome_handler()
    {
        var handler = new OutcomeHandler(() => Task.FromResult(ActionOutcome.Success));

        await Press(handler);

        Assert.Equal(0, handler.ExecuteCalls);
    }

    [Fact]
    public async Task A_failed_binding_does_not_stop_the_next_one()
    {
        var plain = new PlainHandler();

        var failures = await Press(new ThrowingHandler("first"), plain);

        Assert.Single(failures);
        Assert.Equal(["execute"], plain.Calls);
    }

    [Fact]
    public async Task A_failure_text_is_cleaned_and_capped()
    {
        var failures = await Press(new ThrowingHandler("a b‮" + new string('x', 1000)));

        var message = Assert.Single(failures).Message;
        Assert.Equal(200, message.Length);
        Assert.StartsWith("abx", message);
    }

    [Fact]
    public async Task RunAsync_returns_the_failure_for_the_widget_path()
    {
        var dispatcher = new ActionDispatcher([new OutcomeHandler(() => Task.FromResult(ActionOutcome.Failed(ActionFailureCode.Timeout)))], NullLogger<ActionDispatcher>.Instance);

        var failure = await dispatcher.RunAsync("test.outcome", [], Context(), default);
        Assert.Equal(ActionFailureCode.Timeout, failure!.Code);
        Assert.Equal("No answer in time.", failure.Message);

        Assert.Equal(ActionFailureCode.NotFound, (await dispatcher.RunAsync("nope", [], Context(), default))!.Code);
    }

    private sealed class ThrowingReleaseHandler : IActionHandler, IReleaseAwareAction
    {
        public string Type => "test.throwingRelease";
        public string DisplayName => "Throwing release";

        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("release failed");
    }

    [Fact]
    public async Task A_missing_action_is_reported_and_the_next_one_still_runs()
    {
        var plain = new PlainHandler();
        var dispatcher = new ActionDispatcher([plain], NullLogger<ActionDispatcher>.Instance);
        var widget = new Widget { Actions = { [WidgetEvents.Press] = [new ActionBinding("gone.type", []), new ActionBinding(plain.Type, [])] } };

        var failures = await dispatcher.DispatchAsync(widget, WidgetEvents.Press, Context(), default);

        var failure = Assert.Single(failures);
        Assert.True(failure.Missing);
        Assert.Equal(ActionFailureCode.NotFound, failure.Code);
        Assert.Contains("'gone.type' is not available", failure.Message);
        Assert.Single(plain.Calls);
    }
}
