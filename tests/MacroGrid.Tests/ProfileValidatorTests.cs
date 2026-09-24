using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;

namespace MacroGrid.Tests;

public class ProfileValidatorTests
{
    private static Profile Profile(params Widget[] widgets) => new()
    {
        Name = "Test",
        Pages = [new Page { Name = "P1", Cols = 4, Rows = 3, Widgets = [.. widgets] }],
    };

    private static Widget Widget(int x, int y, int w = 1, int h = 1) => new() { X = x, Y = y, W = w, H = h };

    [Fact]
    public void Accepts_non_overlapping_widgets()
    {
        var profile = Profile(Widget(0, 0), Widget(1, 0, 2, 2), Widget(3, 2));

        Assert.True(ProfileValidator.Validate(profile, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_overlapping_widgets()
    {
        var profile = Profile(Widget(0, 0, 2, 2), Widget(1, 1));

        Assert.False(ProfileValidator.Validate(profile, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Rejects_widget_outside_grid_bounds()
    {
        var profile = Profile(Widget(3, 0, 2, 1));

        Assert.False(ProfileValidator.Validate(profile, out _));
    }

    [Fact]
    public void Rejects_negative_position()
    {
        var profile = Profile(Widget(-1, 0));

        Assert.False(ProfileValidator.Validate(profile, out _));
    }

    [Fact]
    public void Rejects_empty_name()
    {
        var profile = Profile();
        profile.Name = "  ";

        Assert.False(ProfileValidator.Validate(profile, out var error));
        Assert.Contains("adı", error);
    }

    [Fact]
    public void Rejects_profile_with_no_pages()
    {
        var profile = new Profile { Name = "Test", Pages = [] };

        Assert.False(ProfileValidator.Validate(profile, out var error));
        Assert.Contains("sayfa", error);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(3, 0)]
    [InlineData(25, 3)]
    public void Rejects_out_of_range_grid_size(int cols, int rows)
    {
        var profile = new Profile { Name = "Test", Pages = [new Page { Name = "P1", Cols = cols, Rows = rows }] };

        Assert.False(ProfileValidator.Validate(profile, out _));
    }

    [Fact]
    public void Rejects_duplicate_widget_ids_on_the_same_page()
    {
        var a = Widget(0, 0);
        var b = Widget(1, 0);
        b.Id = a.Id;
        var profile = Profile(a, b);

        Assert.False(ProfileValidator.Validate(profile, out var error));
        Assert.Contains("Yinelenen widget", error);
    }

    [Fact]
    public void Accepts_distinct_app_matches()
    {
        var profile = Profile();
        profile.AppMatches = [new AppMatch { ProcessName = "Player.exe" }, new AppMatch { ProcessName = "Chat.exe" }];

        Assert.True(ProfileValidator.Validate(profile, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Rejects_app_match_with_empty_process_name()
    {
        var profile = Profile();
        profile.AppMatches = [new AppMatch { ProcessName = "  " }];

        Assert.False(ProfileValidator.Validate(profile, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Rejects_duplicate_app_matches()
    {
        var profile = Profile();
        profile.AppMatches = [new AppMatch { ProcessName = "Player.exe" }, new AppMatch { ProcessName = "player.exe" }];

        Assert.False(ProfileValidator.Validate(profile, out var error));
        Assert.Contains("yinelenen", error);
    }
}
