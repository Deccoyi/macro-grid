using MacroGrid.Core.Plugins;

namespace MacroGrid.Tests;

public sealed class SemVerTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("1.3.4", "1.3.0", true)]
    [InlineData("1.4.0", "1.3.9", true)]
    [InlineData("1.2.5", "1.3.0", false)]
    [InlineData("2.0.0", "1.3.0", false)] // a different major is never compatible, in either direction
    [InlineData("1.9.0", "2.0.0", false)]
    [InlineData("1.0.0-beta", "1.0.0", true)] // a label on the running version is ignored
    [InlineData("1.0.0", "1.0", false)] // the required version is always three parts
    [InlineData("1.0.0", "^1.0.0", false)]
    [InlineData("1.0.0", "", false)]
    public void SatisfiesPlatform_needs_the_same_major_and_at_least_the_required_version(string actual, string required, bool expected) =>
        Assert.Equal(expected, SemVer.SatisfiesPlatform(actual, required));

    [Theory]
    [InlineData("1.0.0", true)]
    [InlineData("10.20.30", true)]
    [InlineData("1.0", false)]
    [InlineData("1.0.0-beta", false)]
    [InlineData("^1.0.0", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsThreePartVersion_accepts_only_major_minor_patch(string? version, bool expected) =>
        Assert.Equal(expected, SemVer.IsThreePartVersion(version));
}
