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
