using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public sealed class CrashReportPolicyTests
{
    private static CrashMarker Marker(CrashPhase phase) => new(
        1, DateTimeOffset.Now, phase, true, "1", "Windows", ".NET", 0, "System.Exception", "x", null, null, 0);

    private static CrashMarkerSet Set(CrashMarker[] markers, int unreadable = 0) =>
        new(markers, Enumerable.Repeat<DateTimeOffset?>(null, unreadable).ToList(), []);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_markers_gives_none(bool editor)
    {
        Assert.Equal(CrashReportTiming.None, CrashReportPolicy.When(CrashMarkerSet.Empty, editor));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Any_starting_marker_gives_before_server_with_and_without_the_editor(bool editor)
    {
        var set = Set([Marker(CrashPhase.Running), Marker(CrashPhase.Starting)]);

        Assert.Equal(CrashReportTiming.BeforeServer, CrashReportPolicy.When(set, editor));
    }

    [Fact]
    public void Only_running_markers_wait_for_the_editor_or_the_tray_menu()
    {
        var set = Set([Marker(CrashPhase.Running)]);

        Assert.Equal(CrashReportTiming.WithEditor, CrashReportPolicy.When(set, true));
        Assert.Equal(CrashReportTiming.OnTrayMenu, CrashReportPolicy.When(set, false));
    }

    [Fact]
    public void An_unreadable_marker_counts_as_running()
    {
        var set = Set([], unreadable: 1);

        Assert.Equal(CrashReportTiming.WithEditor, CrashReportPolicy.When(set, true));
        Assert.Equal(CrashReportTiming.OnTrayMenu, CrashReportPolicy.When(set, false));
    }
}
