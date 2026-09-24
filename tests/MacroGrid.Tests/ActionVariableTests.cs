using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public class ActionVariableTests
{
    private sealed class Recorder(bool allowVariables) : IActionHandler, IActionDescriptor
    {
        public JsonObject? Received { get; private set; }
        public string Type => "test.record";
        public string DisplayName => "Record";
        public string Category => "Test";
        public string? Description => null;
        public string? Icon => null;
        public IReadOnlyList<SettingField> Fields =>
        [
            new("text", "Text", SettingFieldKind.Text) { AllowVariables = allowVariables },
            new("plain", "Plain", SettingFieldKind.Text),
        ];

        public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
        {
            Received = settings;
            return Task.CompletedTask;
        }
    }

    private static (ActionDispatcher Dispatcher, Widget Widget, ActionBinding Binding) Setup(Recorder handler, VariableStore variables)
    {
        var binding = new ActionBinding("test.record", new JsonObject { ["text"] = "CPU {cpu|0}%", ["plain"] = "{cpu}" });
        var widget = new Widget { Actions = { [WidgetEvents.Press] = [binding] } };
        return (new ActionDispatcher([handler], NullLogger<ActionDispatcher>.Instance, variables), widget, binding);
    }

    private static Task<IReadOnlyList<string>> Press(ActionDispatcher dispatcher, Widget widget) =>
        dispatcher.DispatchAsync(widget, WidgetEvents.Press, new ActionContext("d", "p", "w", null!), CancellationToken.None);

    [Fact]
    public async Task Variables_in_fields_that_allow_them_are_resolved_before_the_action_runs()
    {
        var variables = new VariableStore();
        variables.Set("cpu", 42.0);
        var handler = new Recorder(allowVariables: true);
        var (dispatcher, widget, _) = Setup(handler, variables);

        await Press(dispatcher, widget);

        Assert.Equal("CPU 42%", handler.Received!["text"]!.GetValue<string>());
        Assert.Equal("{cpu}", handler.Received["plain"]!.GetValue<string>()); // not marked AllowVariables: left alone
    }

    [Fact]
    public async Task The_saved_binding_is_never_modified()
    {
        var variables = new VariableStore();
        variables.Set("cpu", 42.0);
        var (dispatcher, widget, binding) = Setup(new Recorder(allowVariables: true), variables);

        await Press(dispatcher, widget);

        Assert.Equal("CPU {cpu|0}%", binding.Settings["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task Fields_that_do_not_allow_variables_are_passed_through_untouched()
    {
        var variables = new VariableStore();
        variables.Set("cpu", 42.0);
        var handler = new Recorder(allowVariables: false);
        var (dispatcher, widget, binding) = Setup(handler, variables);

        await Press(dispatcher, widget);

        Assert.Same(binding.Settings, handler.Received);
    }
}
