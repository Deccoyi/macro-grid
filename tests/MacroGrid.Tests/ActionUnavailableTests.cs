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

    [Fact]
    public void A_deleted_page_profile_or_web_widget_target_is_an_unavailable_source()
    {
        var dispatcher = new ActionDispatcher([new PageAction(), new ProfileAction()], NullLogger<ActionDispatcher>.Instance);
        var store = new VariableStore();
        var profile = new Profile { Id = "p", Name = "P", Pages = [new Page { Id = "home", Name = "Home" }] };
        Widget With(string type, JsonObject settings) => new() { Actions = { [WidgetEvents.Press] = [new ActionBinding(type, settings)] } };

        Assert.Empty(dispatcher.UnavailableSources(With(PageAction.TypeId, PageAction.Goto("home")), store, profile));
        Assert.Equal(["deleted page"], dispatcher.UnavailableSources(With(PageAction.TypeId, PageAction.Goto("gone")), store, profile));
        Assert.Empty(dispatcher.UnavailableSources(With(PageAction.TypeId, PageAction.Next()), store, profile));
        Assert.Empty(dispatcher.UnavailableSources(With(PageAction.TypeId, PageAction.Goto("gone")), store)); // no profile known: no judgement
        Assert.Equal(["deleted profile"], dispatcher.UnavailableSources(With(ProfileAction.TypeId, ProfileAction.Settings("x")), store, profile, _ => false));
        Assert.Empty(dispatcher.UnavailableSources(With(ProfileAction.TypeId, ProfileAction.Settings("x")), store, profile, _ => true));
    }

    [Fact]
    public void A_variable_action_on_a_deleted_user_variable_is_an_unavailable_source()
    {
        var dispatcher = new ActionDispatcher([new VariableToggleAction(null!)], NullLogger<ActionDispatcher>.Instance);
        var store = new VariableStore();
        var widget = new Widget { Actions = { [WidgetEvents.Press] = [new ActionBinding(VariableToggleAction.TypeId, new JsonObject { ["variable"] = "user.flag" })] } };

        Assert.Equal(["deleted variable"], dispatcher.UnavailableSources(widget, store, userVariableExists: _ => false));
        Assert.Empty(dispatcher.UnavailableSources(widget, store, userVariableExists: _ => true));
        Assert.Contains("user.flag", dispatcher.ActionDependencies(widget));
    }
}
