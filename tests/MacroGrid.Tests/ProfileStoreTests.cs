using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;

namespace MacroGrid.Tests;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Creates_default_profile_on_first_run()
    {
        var store = new ProfileStore(_dir);

        var profile = Assert.Single(store.All);
        Assert.NotEmpty(profile.Pages[0].Widgets);
        Assert.True(File.Exists(Path.Combine(_dir, "profiles", profile.Id + ".json")));
    }

    [Fact]
    public void Saved_profile_survives_reload()
    {
        var store = new ProfileStore(_dir);
        var profile = store.First();
        var clockWidget = profile.Pages[0].Widgets[0];
        clockWidget.Text = "Değişti {system.time}";
        clockWidget.W = 3;
        store.Save(profile);

        var reloaded = new ProfileStore(_dir).Get(profile.Id)!;
        var reloadedClock = reloaded.Pages[0].FindWidget(clockWidget.Id)!;

        Assert.Equal("Değişti {system.time}", reloadedClock.Text);
        Assert.Equal(3, reloadedClock.W);

        var macroWidget = reloaded.Pages[1].Widgets.Single(w => w.Text == "Tümünü kopyala");
        var bindings = macroWidget.Actions[WidgetEvents.Press];
        Assert.Equal(3, bindings.Count);
        Assert.Equal(HotkeyAction.TypeId, bindings[0].Type);
        Assert.Equal(DelayAction.TypeId, bindings[1].Type);
    }

    [Fact]
    public void Default_profile_has_two_pages_linked_by_page_actions()
    {
        var profile = new ProfileStore(_dir).First();

        Assert.Equal(2, profile.Pages.Count);
        var toPage2 = profile.Pages[0].Widgets.Single(w => w.Text == "Sayfa 2 →");
        Assert.Equal(profile.Pages[1].Id, toPage2.Actions[WidgetEvents.Press][0].Settings["pageId"]!.GetValue<string>());

        var back = profile.Pages[1].Widgets.Single(w => w.Text == "← Geri");
        Assert.Equal(PageAction.TypeId, back.Actions[WidgetEvents.Press][0].Type);
    }

    [Fact]
    public void Broken_file_is_set_aside_instead_of_crashing()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "profiles"));
        File.WriteAllText(Path.Combine(_dir, "profiles", "bad.json"), "{ not json");

        var store = new ProfileStore(_dir);

        Assert.Single(store.All);
        Assert.True(File.Exists(Path.Combine(_dir, "profiles", "bad.json.broken")));
    }

    [Fact]
    public void Rejects_path_traversal_ids()
    {
        var store = new ProfileStore(_dir);

        Assert.Throws<ArgumentException>(() => store.Save(new Profile { Id = "..\\evil" }));
    }

    [Fact]
    public void Delete_removes_a_profile_but_never_the_last_one()
    {
        var store = new ProfileStore(_dir);
        var first = store.First();
        var second = new Profile { Name = "İkinci" };
        store.Save(second);

        Assert.True(store.Delete(second.Id));
        Assert.Null(store.Get(second.Id));

        Assert.False(store.Delete(first.Id));
        Assert.NotNull(store.Get(first.Id));
    }
}
