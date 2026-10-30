using MacroGrid.Core.Diagnostics;

namespace MacroGrid.Tests;

public class PlainTextTests
{
    [Fact]
    public void Removes_control_direction_and_zero_width_characters()
    {
        Assert.Equal("abc", PlainText.Clean("a\u0000b‮c​⁧", 50));
    }

    [Fact]
    public void Cuts_to_the_cap()
    {
        Assert.Equal("abc", PlainText.Clean("abcdef", 3));
        Assert.Equal("", PlainText.Clean(null, 3));
    }

    [Fact]
    public void Never_cuts_a_surrogate_pair_in_half()
    {
        var cleaned = PlainText.Clean("ab😀", 3);
        Assert.Equal("ab", cleaned);
        Assert.Equal("ab😀", PlainText.Clean("ab😀", 4));
    }
}
