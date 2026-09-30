using System.Diagnostics;
using MacroGrid.Windows.Windows;

namespace MacroGrid.Tests;

public class ForegroundWindowMonitorTests
{
    [Fact]
    public void The_image_name_of_a_running_process_matches_its_process_name()
    {
        using var self = Process.GetCurrentProcess();

        Assert.Equal(self.ProcessName + ".exe", ForegroundWindowMonitor.ImageNameFor((uint)self.Id), ignoreCase: true);
    }

    [Fact]
    public void A_process_that_does_not_exist_has_no_image_name()
    {
        Assert.Null(ForegroundWindowMonitor.ImageNameFor(uint.MaxValue - 1));
    }
}
