using MacroGrid.Windows.Autostart;
using Microsoft.Win32;

namespace MacroGrid.Tests;

public sealed class AutostartServiceTests : IDisposable
{
    // A throw-away key, so the tests never touch the real Run entry of the person running them.
    private readonly string _keyPath = @"Software\MacroGridTests\" + Guid.NewGuid().ToString("N");

    private AutostartService NewService(string exe = @"C:\Program Files\Macro Grid\MacroGrid.exe") => new(exe, _keyPath);

    [Fact]
    public void It_is_off_when_the_value_does_not_exist()
    {
        Assert.False(NewService().IsEnabled());
    }

    [Fact]
    public void Enabling_writes_the_quoted_exe_path_with_the_autostart_argument_and_disabling_removes_it()
    {
        var service = NewService(@"C:\Program Files\Macro Grid\MacroGrid.exe");

        service.SetEnabled(true);

        Assert.True(service.IsEnabled());
        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath))
            Assert.Equal("\"C:\\Program Files\\Macro Grid\\MacroGrid.exe\" --autostart", key!.GetValue("MacroGrid"));

        service.SetEnabled(false);

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void Disabling_twice_or_when_never_enabled_does_not_throw()
    {
        var service = NewService();

        service.SetEnabled(false);
        service.SetEnabled(false);

        Assert.False(service.IsEnabled());
    }

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(@"Software\MacroGridTests", throwOnMissingSubKey: false);
}
