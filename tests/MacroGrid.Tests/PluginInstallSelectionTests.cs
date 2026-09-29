using MacroGrid.Core.Plugins;

namespace MacroGrid.Tests;

public sealed class PluginInstallSelectionTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "picked-plugin");

    [Fact]
    public void The_picked_folder_is_returned_once()
    {
        var selection = new PluginInstallSelection();
        selection.Set(Folder);

        Assert.Equal(Path.GetFullPath(Folder), selection.Take(Folder));
        Assert.Null(selection.Take(Folder)); // used up
    }

    [Fact]
    public void A_path_the_person_did_not_pick_is_refused()
    {
        var selection = new PluginInstallSelection();
        selection.Set(Folder);

        Assert.Null(selection.Take(Path.Combine(Path.GetTempPath(), "somewhere-else")));
    }

    [Fact]
    public void Nothing_picked_or_nothing_claimed_is_refused()
    {
        var selection = new PluginInstallSelection();
        Assert.Null(selection.Take(Folder));

        selection.Set(Folder);
        Assert.Null(selection.Take(null));
    }

    [Fact]
    public void A_new_pick_replaces_the_old_one_and_the_comparison_ignores_case()
    {
        var selection = new PluginInstallSelection();
        selection.Set(Path.Combine(Path.GetTempPath(), "first"));
        selection.Set(Folder);

        Assert.Equal(Path.GetFullPath(Folder), selection.Take(Folder.ToUpperInvariant()));
    }
}
