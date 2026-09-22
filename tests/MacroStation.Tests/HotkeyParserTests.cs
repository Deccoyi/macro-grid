using MacroStation.Core.Input;
using MacroStation.Plugin.Abstractions;
using MacroStation.Windows.Input;

namespace MacroStation.Tests;

public class HotkeyParserTests
{
    [Theory]
    [InlineData("ctrl+c", KeyModifiers.Ctrl, "c")]
    [InlineData("Ctrl + Shift + S", KeyModifiers.Ctrl | KeyModifiers.Shift, "s")]
    [InlineData("alt+F4", KeyModifiers.Alt, "f4")]
    [InlineData("control+esc", KeyModifiers.Ctrl, "escape")]
    [InlineData("cmd+plus", KeyModifiers.Win, "plus")]
    [InlineData("mediaplaypause", KeyModifiers.None, "mediaplaypause")]
    [InlineData("win", KeyModifiers.None, "win")]
    public void Parses_valid_shortcuts(string text, KeyModifiers modifiers, string key)
    {
        var combo = HotkeyParser.Parse(text);

        Assert.Equal(modifiers, combo.Modifiers);
        Assert.Equal(key, combo.Key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ctrl++")]
    [InlineData("ctrl+a+b")]
    [InlineData("ctrl+shift")]
    [InlineData("ctrl+foo")]
    public void Rejects_invalid_shortcuts(string text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _, out var error));
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Round_trips_through_ToString()
    {
        Assert.Equal("ctrl+shift+s", HotkeyParser.Parse("Shift+Ctrl+S").ToString());
    }

    [Fact]
    public void Every_known_key_has_a_windows_virtual_key()
    {
        var missing = KnownKeys.All.Where(k => !VirtualKeys.Map.ContainsKey(k)).ToList();

        Assert.Empty(missing);
    }
}
