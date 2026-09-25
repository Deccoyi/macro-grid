using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class InstallLocationTests
{
    [Theory]
    [InlineData(@"C:\Program Files\Macro Grid\", @"C:\Program Files\Macro Grid", true)]
    [InlineData(@"C:\Program Files\Macro Grid", @"C:\Program Files\Macro Grid\", true)]
    [InlineData(@"c:\program files\macro grid\", @"C:\Program Files\Macro Grid\", true)]
    [InlineData(@"C:\Program Files\Macro Grid\", @"C:\Users\me\Downloads\MacroGrid", false)]
    [InlineData(@"C:\Program Files\Macro Grid\", @"C:\Program Files\Macro Grid\plugins", false)]
    [InlineData(null, @"C:\Program Files\Macro Grid", false)]
    [InlineData("", @"C:\Program Files\Macro Grid", false)]
    public void Compares_the_recorded_folder_with_the_running_one(string? recorded, string running, bool expected) =>
        Assert.Equal(expected, InstallLocation.IsSameFolder(recorded, running));
}
