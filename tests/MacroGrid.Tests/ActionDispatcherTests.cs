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

        Assert.Equal(["release failed"], errors);
        Assert.Equal(["execute"], releaseHandler.Calls);
    }

    private sealed class ThrowingReleaseHandler : IActionHandler, IReleaseAwareAction
    {
        public string Type => "test.throwingRelease";
        public string DisplayName => "Throwing release";

        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("release failed");
    }
}
