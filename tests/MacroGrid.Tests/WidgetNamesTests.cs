using System.Text.Json;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Protocol;

namespace MacroGrid.Tests;

public class WidgetNamesTests
{
    public sealed record CaseWidget(string Type, string? Name = null);

    public sealed record NameCase(string Name, List<CaseWidget> Widgets, List<string> Expected);

    public static IEnumerable<object[]> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "shared", "widget-name-cases.json");
        var cases = JsonSerializer.Deserialize<List<NameCase>>(File.ReadAllText(path), ProtocolJson.Options)!;
        return cases.Select(c => new object[] { c });
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Shared_table(NameCase c)
    {
        var page = new Page { Widgets = c.Widgets.Select(w => new Widget { Type = w.Type, Name = w.Name }).ToList() };

        WidgetNames.Ensure(page);

        Assert.Equal(c.Expected, page.Widgets.Select(w => w.Name!).ToList());
    }

    [Fact]
    public void A_second_run_changes_nothing()
    {
        var page = new Page { Widgets = [new Widget { Name = "A" }, new Widget { Name = "a" }, new Widget(), new Widget { Type = WidgetTypes.Label }] };
        Assert.True(WidgetNames.Ensure(page));
        Assert.False(WidgetNames.Ensure(page));
    }

    [Fact]
    public void A_profile_is_checked_page_by_page_so_the_same_name_on_two_pages_is_fine()
    {
        var profile = new Profile { Pages = [new Page { Widgets = [new Widget { Name = "Go" }] }, new Page { Widgets = [new Widget { Name = "Go" }] }] };
        Assert.False(WidgetNames.Ensure(profile));
    }

    [Fact]
    public void Default_and_unique_helpers()
    {
        Assert.Equal("Button_3", WidgetNames.Default(WidgetTypes.Button, ["Button_1", "button_2"]));
        Assert.Equal("Go_2", WidgetNames.Unique("Go", ["go"]));
        Assert.Equal("Go", WidgetNames.Unique("Go", ["Stop"]));
    }
}
