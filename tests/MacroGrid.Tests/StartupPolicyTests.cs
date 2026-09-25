using MacroGrid.Core.Preferences;

namespace MacroGrid.Tests;

public sealed class StartupPolicyTests
{
    private static readonly string[] Manual = [];
    private static readonly string[] FromWindows = ["--autostart"];

    [Fact]
    public void By_default_a_start_by_the_person_opens_the_window()
    {
        Assert.True(StartupPolicy.ShouldOpenEditor(Manual, new AppPreferences()));
    }

    [Fact]
    public void By_default_a_start_by_windows_stays_in_the_tray()
    {
        Assert.False(StartupPolicy.ShouldOpenEditor(FromWindows, new AppPreferences()));
    }

    [Fact]
    public void A_start_by_the_person_can_be_set_to_the_tray()
    {
        Assert.False(StartupPolicy.ShouldOpenEditor(Manual, new AppPreferences { LaunchMode = StartupPolicy.Tray }));
    }

    [Fact]
    public void A_start_by_windows_can_be_set_to_open_the_window()
    {
        Assert.True(StartupPolicy.ShouldOpenEditor(FromWindows, new AppPreferences { AutostartMode = StartupPolicy.Window }));
    }

    [Fact]
    public void The_two_modes_are_independent()
    {
        var prefs = new AppPreferences { LaunchMode = StartupPolicy.Tray, AutostartMode = StartupPolicy.Window };

        Assert.False(StartupPolicy.ShouldOpenEditor(Manual, prefs));
        Assert.True(StartupPolicy.ShouldOpenEditor(FromWindows, prefs));
    }

    [Fact]
    public void The_argument_is_matched_without_regard_to_case_and_unknown_values_fall_back_to_the_defaults()
    {
        var prefs = new AppPreferences { LaunchMode = "nonsense", AutostartMode = "nonsense" };

        Assert.True(StartupPolicy.ShouldOpenEditor(Manual, prefs));
        Assert.False(StartupPolicy.ShouldOpenEditor(["--AUTOSTART"], prefs));
    }

    [Theory]
    [InlineData("0.2.1", "0.3.0", true)]
    [InlineData("0.3.0", "0.3.0", false)]
    [InlineData("0.3.1", "0.3.0", false)]
    [InlineData(null, "0.3.0", false)]
    [InlineData("nonsense", "0.3.0", false)]
    public void A_higher_version_than_last_time_means_the_app_was_just_updated(string? lastRun, string current, bool expected) =>
        Assert.Equal(expected, StartupPolicy.WasUpdated(Manual, lastRun, current));

    [Fact]
    public void The_updated_argument_still_counts_without_a_version_change()
    {
        Assert.True(StartupPolicy.WasUpdated(["--updated"], "0.3.0", "0.3.0"));
    }

    [Fact]
    public void An_update_restart_is_recognised_and_still_follows_the_launch_mode()
    {
        Assert.True(StartupPolicy.WasUpdated(["--updated"]));
        Assert.True(StartupPolicy.WasUpdated(["--UPDATED"]));
        Assert.False(StartupPolicy.WasUpdated(Manual));
        Assert.True(StartupPolicy.ShouldOpenEditor(["--updated"], new AppPreferences()));
        Assert.False(StartupPolicy.ShouldOpenEditor(["--updated"], new AppPreferences { LaunchMode = StartupPolicy.Tray }));
    }
}
