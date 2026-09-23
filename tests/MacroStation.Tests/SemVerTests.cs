using MacroStation.Core.Plugins;

namespace MacroStation.Tests;

public sealed class SemVerTests
{
    [Theory]
    [InlineData("1.2.3", "1.2.3", true)]
    [InlineData("1.3.0", "1.2.3", true)]
    [InlineData("1.2.2", "1.2.3", false)]
    public void SatisfiesMinimum_compares_major_minor_patch(string actual, string minimum, bool expected) =>
        Assert.Equal(expected, SemVer.SatisfiesMinimum(actual, minimum));

    [Theory]
    [InlineData("1.5.0", "^1.0.0", true)]
    [InlineData("2.0.0", "^1.0.0", false)]
    [InlineData("0.9.0", "^1.0.0", false)]
    [InlineData("0.1.5", "^0.1.0", true)] // 0.x caret only tolerates patch bumps
    [InlineData("0.2.0", "^0.1.0", false)]
    public void SatisfiesCaret_follows_npm_caret_rules(string actual, string range, bool expected) =>
        Assert.Equal(expected, SemVer.SatisfiesCaret(actual, range));
}
