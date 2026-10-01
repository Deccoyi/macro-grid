using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class SetVariableActionTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-setvar-" + Guid.NewGuid().ToString("N"));
    private readonly VariableStore _store = new();
    private readonly UserVariableService _service;
    private readonly SetVariableAction _action;

    public SetVariableActionTests()
    {
        _service = new UserVariableService(_dir, _store);
        _service.TryReplace(
        [
            new("count", VariableType.Number, 5),
            new("flag", VariableType.Boolean, false),
            new("title", VariableType.Text, "a"),
        ], out _);
        _action = new SetVariableAction(_service);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static ActionContext Context(double? value = null) => new("dev", "page", "widget", new FakeDeviceController(), value);

    private Task<ActionOutcome> Run(JsonObject settings, double? value = null) =>
        _action.ExecuteWithOutcomeAsync(Context(value), settings, CancellationToken.None);

    private static JsonObject S(string variable, string mode, string? value = null, double? amount = null)
    {
        var o = new JsonObject { ["variable"] = variable, ["mode"] = mode };
        if (value is not null) o["value"] = value;
        if (amount is not null) o["amount"] = amount;
        return o;
    }

    [Fact]
    public async Task Set_writes_the_value_converted_to_the_type()
    {
        Assert.Equal(ActionOutcomeKind.Success, (await Run(S("user.count", "set", "12"))).Kind);
        Assert.Equal(12.0, _store.Get("user.count"));
        await Run(S("user.title", "set", "hello"));
        Assert.Equal("hello", _store.Get("user.title"));
    }

    [Fact]
    public async Task An_empty_value_writes_the_dragged_number()
    {
        await Run(S("user.count", "set", ""), value: 73);

        Assert.Equal(73.0, _store.Get("user.count"));
    }

    [Fact]
    public async Task An_empty_value_clears_a_text_variable_but_fails_for_a_number()
    {
        await Run(S("user.title", "set", ""));
        Assert.Equal("", _store.Get("user.title"));

        var outcome = await Run(S("user.count", "set", ""));
        Assert.Equal(ActionFailureCode.InvalidParameter, outcome.Code);
        Assert.Equal(5.0, _store.Get("user.count"));
    }

    [Fact]
    public async Task Toggle_add_and_reset_work()
    {
        await Run(S("user.flag", "toggle"));
        Assert.Equal(true, _store.Get("user.flag"));
        await Run(S("user.count", "add"));
        Assert.Equal(6.0, _store.Get("user.count"));
        await Run(S("user.count", "add", amount: -10));
        Assert.Equal(-4.0, _store.Get("user.count"));
        await Run(S("user.count", "reset"));
        Assert.Equal(5.0, _store.Get("user.count"));
    }

    [Fact]
    public async Task Wrong_type_and_bad_values_fail_with_invalid_parameter()
    {
        Assert.Equal(ActionFailureCode.InvalidParameter, (await Run(S("user.count", "toggle"))).Code);
        Assert.Equal(ActionFailureCode.InvalidParameter, (await Run(S("user.flag", "add"))).Code);
        Assert.Equal(ActionFailureCode.InvalidParameter, (await Run(S("user.count", "set", "abc"))).Code);
        Assert.Equal(ActionFailureCode.InvalidParameter, (await Run(S("user.count", "spin"))).Code);
    }

    [Fact]
    public async Task An_unknown_or_foreign_variable_is_not_found_and_an_empty_one_is_not_configured()
    {
        _store.Set("system.cpu", 1.0);

        Assert.Equal(ActionFailureCode.NotFound, (await Run(S("user.nope", "set", "1"))).Code);
        Assert.Equal(ActionFailureCode.NotFound, (await Run(S("system.cpu", "set", "2"))).Code);
        Assert.Equal(1.0, _store.Get("system.cpu"));
        Assert.Equal(ActionFailureCode.NotConfigured, (await Run(S("", "set", "1"))).Code);
    }

    [Fact]
    public async Task ExecuteAsync_throws_on_failure()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _action.ExecuteAsync(Context(), S("user.nope", "reset"), CancellationToken.None));
    }

    [Fact]
    public async Task The_dispatcher_fills_templates_in_the_value_before_writing()
    {
        _store.Set("demo.level", 40.0);
        var binding = new ActionBinding(SetVariableAction.TypeId, S("user.count", "set", "{demo.level}"));
        var widget = new Widget { Actions = { [WidgetEvents.Press] = [binding] } };
        var dispatcher = new ActionDispatcher([_action], NullLogger<ActionDispatcher>.Instance, _store);

        await dispatcher.DispatchAsync(widget, WidgetEvents.Press, Context(), CancellationToken.None);

        Assert.Equal(40.0, _store.Get("user.count"));
    }
}
