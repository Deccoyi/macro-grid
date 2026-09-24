using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;

namespace MacroGrid.Tests;

public class DynamicTextTests
{
    private static Widget Dynamized(string? staticText, string variable, string op, string value, string result, string? fallback = null) => new()
    {
        Text = staticText,
        Dynamic =
        {
            [DynamicText.PropertyKey] = new DynamicBinding
            {
                Cases = [new DynamicCase(new ConditionNode { Kind = ConditionKinds.Compare, Variable = variable, Operator = op, Value = value }, result)],
                Default = fallback,
            },
        },
    };

    private static VariableStore StoreWith(string name, object? value)
    {
        var store = new VariableStore();
        store.Set(name, value);
        return store;
    }

    [Fact]
    public void A_static_text_without_variables_needs_no_push()
    {
        Assert.Null(DynamicText.Resolve(new Widget { Text = "Hello" }, new VariableStore()));
    }

    [Fact]
    public void A_plain_template_is_rendered()
    {
        Assert.Equal("CPU 42%", DynamicText.Resolve(new Widget { Text = "CPU {cpu|0}%" }, StoreWith("cpu", 42.0)));
    }

    [Fact]
    public void The_matching_case_picks_the_text()
    {
        var widget = Dynamized("idle", "state", "==", "on", "Running");

        Assert.Equal("Running", DynamicText.Resolve(widget, StoreWith("state", "on")));
    }

    [Fact]
    public void A_case_result_may_contain_variables()
    {
        var widget = Dynamized("idle", "cpu", ">", "80", "Hot: {cpu|0}%");

        Assert.Equal("Hot: 91%", DynamicText.Resolve(widget, StoreWith("cpu", 91.0)));
    }

    [Fact]
    public void No_match_falls_back_to_the_default_then_to_the_static_text()
    {
        Assert.Equal("Cool", DynamicText.Resolve(Dynamized("idle", "cpu", ">", "80", "Hot", fallback: "Cool"), StoreWith("cpu", 10.0)));
        Assert.Equal("idle", DynamicText.Resolve(Dynamized("idle", "cpu", ">", "80", "Hot"), StoreWith("cpu", 10.0)));
    }

    [Fact]
    public void A_dynamized_widget_always_pushes_so_the_client_drops_a_stale_rule_result()
    {
        Assert.Equal("idle", DynamicText.Resolve(Dynamized("idle", "cpu", ">", "80", "Hot"), StoreWith("cpu", 10.0)));
        Assert.Equal("", DynamicText.Resolve(Dynamized(null, "cpu", ">", "80", "Hot"), StoreWith("cpu", 10.0)));
    }

    [Fact]
    public void Dependencies_cover_conditions_and_every_candidate_template()
    {
        var widget = Dynamized("{a}", "b", ">", "1", "{c}", fallback: "{d}");

        Assert.Equal(["a", "b", "c", "d"], DynamicText.Dependencies(widget).Order());
    }
}
