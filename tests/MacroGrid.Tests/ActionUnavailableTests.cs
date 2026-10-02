using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public class ActionUnavailableTests
{
    private sealed class Reader : IActionHandler, IActionDescriptor
    {
        public string Type => "test.read";
        public string DisplayName => "Read";
        public string Category => "Test";
        public string? Description => null;
        public string? Icon => null;
        public IReadOnlyList<SettingField> Fields => [new("text", "Text", SettingFieldKind.Text) { AllowVariables = true }];
        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static Widget WidgetWith(string type, string text) => new()
    {
        Actions = { [WidgetEvents.Press] = [new ActionBinding(type, new JsonObject { ["text"] = text })] },
    };

    [Fact]
    public void An_action_type_that_is_not_registered_is_an_unavailable_source_until_it_is()
    {
        var dispatcher = new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance);
        var widget = WidgetWith("obs.startStream", "");
        var store = new VariableStore();

        Assert.Equal(["obs.startStream"], dispatcher.UnavailableSources(widget, store));
        dispatcher.Register(new Reader());
        Assert.Empty(dispatcher.UnavailableSources(WidgetWith("test.read", ""), store));
        Assert.Equal(["test.nope"], dispatcher.UnavailableSources(WidgetWith("test.nope", ""), store));
    }

    [Fact]
    public void A_variable_read_by_an_action_without_a_value_is_unavailable_and_a_placeholder_covers_it()
    {
        var dispatcher = new ActionDispatcher([new Reader()], NullLogger<ActionDispatcher>.Instance);
        var store = new VariableStore();

        Assert.Equal(["obs.scene"], dispatcher.UnavailableSources(WidgetWith("test.read", "Scene {obs.scene}"), store));
        Assert.Empty(dispatcher.UnavailableSources(WidgetWith("test.read", "Scene {obs.scene|text|none}"), store));
        Assert.Contains("obs.scene", dispatcher.ActionDependencies(WidgetWith("test.read", "{obs.scene|text|none}")));

        store.Set("obs.scene", "Main");
        Assert.Empty(dispatcher.UnavailableSources(WidgetWith("test.read", "Scene {obs.scene}"), store));
    }

    [Fact]
    public void Logic_steps_and_widgets_that_opt_out_are_never_unavailable()
    {
        var dispatcher = new ActionDispatcher([], NullLogger<ActionDispatcher>.Instance);
        Assert.Empty(dispatcher.UnavailableSources(WidgetWith(ActionFlow.If, ""), new VariableStore()));

        var optOut = WidgetWith("obs.startStream", "");
        optOut.Props = new JsonObject { ["hideUnavailable"] = true };
        Assert.Empty(dispatcher.UnavailableSources(optOut, new VariableStore()));
    }
}
