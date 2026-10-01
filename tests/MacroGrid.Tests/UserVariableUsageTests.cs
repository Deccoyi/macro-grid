using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Core.Variables;

namespace MacroGrid.Tests;

public class UserVariableUsageTests
{
    private static Profile ProfileWith(Widget widget) =>
        new() { Id = "p1", Name = "Main", Pages = [new Page { Id = "pg1", Name = "Home", Widgets = [widget] }] };

    private static Widget W(string name, string? text = null) => new() { Id = name + "-id", Name = name, Text = text };

    [Fact]
    public void A_text_template_is_found_and_a_longer_name_is_not()
    {
        var profile = ProfileWith(W("a", "Count: {user.count|0}"));

        Assert.Single(UserVariableUsage.Find([profile], "user.count"));
        Assert.Empty(UserVariableUsage.Find([profile], "user.coun"));
        Assert.Empty(UserVariableUsage.Find([ProfileWith(W("b", "{user.counter}"))], "user.count"));
    }

    [Fact]
    public void The_match_ignores_case_and_reports_where_it_is()
    {
        var widget = W("a", "{USER.Count}");
        widget.Actions[WidgetEvents.Press] = [new ActionBinding("core.setVariable", new JsonObject { ["variable"] = "user.count", ["mode"] = "add" })];
        widget.Props = new JsonObject { ["value"] = "{user.count}" };

        var spots = UserVariableUsage.Find([ProfileWith(widget)], "user.count").Select(u => u.Spot).ToList();

        Assert.Equal([UserVariableSpot.Text, UserVariableSpot.Action, UserVariableSpot.Props], spots);
    }

    [Fact]
    public void A_dynamic_rule_is_found()
    {
        var widget = W("a");
        widget.Dynamic["style.background"] = new DynamicBinding { Default = "{user.color}" };

        Assert.Equal(UserVariableSpot.Dynamic, UserVariableUsage.Find([ProfileWith(widget)], "user.color").Single().Spot);
    }

    [Fact]
    public void At_most_fifty_uses_are_returned()
    {
        var page = new Page { Id = "pg", Name = "P", Widgets = [.. Enumerable.Range(0, 80).Select(i => W("w" + i, "{user.n}"))] };

        Assert.Equal(UserVariableUsage.MaxResults, UserVariableUsage.Find([new Profile { Pages = [page] }], "user.n").Count);
    }
}
