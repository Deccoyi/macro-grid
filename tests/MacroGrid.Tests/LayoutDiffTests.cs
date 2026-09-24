using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Core.Sessions;
using MacroGrid.Protocol;

namespace MacroGrid.Tests;

public class LayoutDiffTests
{
    private static JsonObject Node(Profile profile) =>
        JsonSerializer.SerializeToNode(profile, ProtocolJson.Options)!.AsObject();

    private static Profile TwoPageProfile() => new()
    {
        Id = "p1",
        Name = "Main",
        Pages =
        [
            new Page { Id = "a", Name = "A", Widgets = [new Widget { Id = "w1", Text = "one" }, new Widget { Id = "w2", Text = "two" }] },
            new Page { Id = "b", Name = "B", Widgets = [new Widget { Id = "w3", Text = "three" }] },
        ],
    };

    [Fact]
    public void An_identical_profile_produces_no_patch()
    {
        var result = LayoutDiff.Compute(Node(TwoPageProfile()), Node(TwoPageProfile()), "a");

        Assert.Null(result.Patch);
        Assert.Empty(result.ChangedWidgetIds);
    }

    [Fact]
    public void An_edited_widget_is_the_only_thing_in_the_patch()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages[0].Widgets[1].Text = "changed";

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        var page = Assert.Single(result.Patch!["pages"]!.AsArray());
        Assert.Equal("a", page!["id"]!.GetValue<string>());
        Assert.Null(page["meta"]);
        Assert.Null(page["order"]);
        var upsert = Assert.Single(page["widgets"]!.AsArray());
        Assert.Equal("w2", upsert!["id"]!.GetValue<string>());
        Assert.Equal(["w2"], result.ChangedWidgetIds);
    }

    [Fact]
    public void An_added_widget_sends_the_new_order()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages[0].Widgets.Insert(0, new Widget { Id = "fresh" });

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        var page = Assert.Single(result.Patch!["pages"]!.AsArray());
        Assert.Equal(["fresh", "w1", "w2"], page!["order"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Equal(["fresh"], result.ChangedWidgetIds);
    }

    [Fact]
    public void A_removed_widget_is_reported_as_changed_and_dropped_from_the_order()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages[0].Widgets.RemoveAt(0);

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        var page = Assert.Single(result.Patch!["pages"]!.AsArray());
        Assert.Equal(["w2"], page!["order"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Empty(page["widgets"]!.AsArray());
        Assert.Equal(["w1"], result.ChangedWidgetIds);
    }

    [Fact]
    public void A_page_setting_change_sends_only_the_page_meta()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages[1].Cols = 6;

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        var page = Assert.Single(result.Patch!["pages"]!.AsArray());
        Assert.Equal("b", page!["id"]!.GetValue<string>());
        Assert.Equal(6, page["meta"]!["cols"]!.GetValue<int>());
        Assert.Empty(page["widgets"]!.AsArray());
        Assert.Empty(result.ChangedWidgetIds);
    }

    [Fact]
    public void A_new_page_comes_with_its_meta_order_and_widgets()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages.Add(new Page { Id = "c", Name = "C", Widgets = [new Widget { Id = "w9" }] });

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        Assert.Equal(["a", "b", "c"], result.Patch!["pageOrder"]!.AsArray().Select(n => n!.GetValue<string>()));
        var page = Assert.Single(result.Patch["pages"]!.AsArray());
        Assert.Equal("c", page!["id"]!.GetValue<string>());
        Assert.NotNull(page["meta"]);
        Assert.Single(page["widgets"]!.AsArray());
        Assert.Equal(["w9"], result.ChangedWidgetIds);
    }

    [Fact]
    public void A_removed_page_drops_out_of_the_page_order_and_reports_its_widgets()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages.RemoveAt(1);

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        Assert.Equal(["a"], result.Patch!["pageOrder"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Empty(result.Patch["pages"]!.AsArray());
        Assert.Equal(["w3"], result.ChangedWidgetIds);
    }

    [Fact]
    public void A_rename_alone_is_a_patch_with_the_name()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Name = "Renamed";

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        Assert.Equal("Renamed", result.Patch!["name"]!.GetValue<string>());
        Assert.Empty(result.Patch["pages"]!.AsArray());
    }

    [Fact]
    public void Reordering_pages_is_a_patch_with_the_new_page_order_and_no_page_bodies()
    {
        var before = Node(TwoPageProfile());
        var edited = TwoPageProfile();
        edited.Pages.Reverse();

        var result = LayoutDiff.Compute(before, Node(edited), "a");

        Assert.Equal(["b", "a"], result.Patch!["pageOrder"]!.AsArray().Select(n => n!.GetValue<string>()));
        Assert.Empty(result.Patch["pages"]!.AsArray());
    }
}
