using MacroStation.Core.Sessions;

namespace MacroStation.Tests;

public class AutoSwitchStateTests
{
    [Fact]
    public void Undefined_window_is_a_no_op()
    {
        var state = new AutoSwitchState();
        state.OnManual("base");

        var result = state.OnForeground("Explorer.exe", profileId: null);

        Assert.False(result.Changed);
        Assert.Equal("base", state.CurrentProfileId);
    }

    [Fact]
    public void Rule_match_switches_and_closing_it_returns_to_the_previous_profile()
    {
        var state = new AutoSwitchState();
        state.OnManual("stream");

        var toSpotify = state.OnForeground("Spotify.exe", "spotify-profile");
        Assert.True(toSpotify.Changed);
        Assert.Equal("spotify-profile", toSpotify.ProfileId);
        Assert.Equal("spotify-profile", state.CurrentProfileId);

        // Spotify no longer has a visible window (closed, or tray-minimized and gone).
        var afterClose = state.Prune(hasVisibleWindow: _ => false);
        Assert.True(afterClose.Changed);
        Assert.Equal("stream", afterClose.ProfileId);
        Assert.Equal("stream", state.CurrentProfileId);
    }

    [Fact]
    public void Manual_pick_then_rule_then_close_returns_to_the_manual_pick()
    {
        var state = new AutoSwitchState();
        state.OnManual("stream");
        state.OnForeground("Spotify.exe", "spotify-profile");

        var afterClose = state.Prune(hasVisibleWindow: _ => false);

        Assert.Equal("stream", afterClose.ProfileId);
    }

    [Fact]
    public void Empty_stack_after_pruning_the_only_entry_asks_the_caller_for_the_default()
    {
        var state = new AutoSwitchState();
        // No manual base at all — only a rule entry (e.g. this session never had a prior pick this run).
        state.OnForeground("Spotify.exe", "spotify-profile");

        var result = state.Prune(hasVisibleWindow: _ => false);

        Assert.True(result.Changed);
        Assert.True(result.ToDefault);
        Assert.Null(result.ProfileId);
    }

    [Fact]
    public void Locked_ignores_foreground_events_but_manual_switches_still_apply()
    {
        var state = new AutoSwitchState();
        state.OnManual("stream");
        state.SetLocked(true);

        var whileLocked = state.OnForeground("Spotify.exe", "spotify-profile");
        Assert.False(whileLocked.Changed);
        Assert.Equal("stream", state.CurrentProfileId);

        state.OnManual("other"); // e.g. a drawer pick while locked
        Assert.Equal("other", state.CurrentProfileId);
    }

    [Fact]
    public void Same_rule_matching_again_while_already_on_top_is_a_no_op()
    {
        var state = new AutoSwitchState();
        state.OnManual("stream");
        state.OnForeground("Spotify.exe", "spotify-profile");

        var again = state.OnForeground("Spotify.exe", "spotify-profile");

        Assert.False(again.Changed);
    }

    [Fact]
    public void Switching_back_to_an_app_already_deeper_in_the_stack_moves_it_to_the_top()
    {
        var state = new AutoSwitchState();
        state.OnManual("stream");
        state.OnForeground("Spotify.exe", "spotify-profile");
        state.OnForeground("Discord.exe", "discord-profile");

        // Spotify comes back to the foreground — its existing stack entry should move to the top rather
        // than stacking a duplicate.
        var backToSpotify = state.OnForeground("Spotify.exe", "spotify-profile");
        Assert.True(backToSpotify.Changed);
        Assert.Equal("spotify-profile", state.CurrentProfileId);

        // Closing Discord now shouldn't resurface it (it was pruned off already) or change anything.
        var pruneDiscord = state.Prune(hasVisibleWindow: p => p == "Spotify.exe");
        Assert.False(pruneDiscord.Changed);
        Assert.Equal("spotify-profile", state.CurrentProfileId);
    }
}
