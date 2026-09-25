using MacroGrid.Core.Updates;

namespace MacroGrid.Tests;

public sealed class ReleaseVersionTests
{
    private static ReleaseVersion V(string text) => ReleaseVersion.TryParse(text, out var v) ? v : throw new FormatException(text);

    [Theory]
    [InlineData("0.2.1", 0, 2, 1, null)]
    [InlineData("v0.3.0", 0, 3, 0, null)]
    [InlineData("0.3.0-alpha", 0, 3, 0, "alpha")]
    [InlineData("1.0.0-alpha.2", 1, 0, 0, "alpha.2")]
    [InlineData("1.0.0+build5", 1, 0, 0, null)]
    public void Parses_versions(string text, int major, int minor, int patch, string? label)
    {
        Assert.True(ReleaseVersion.TryParse(text, out var version));
        Assert.Equal(new ReleaseVersion(major, minor, patch, label), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("a.b.c")]
    [InlineData("1.2.3.4")]
    [InlineData("99999999999.0.0")]
    public void Rejects_malformed_versions(string text) => Assert.False(ReleaseVersion.TryParse(text, out _));

    [Fact]
    public void Tag_needs_the_prefix()
    {
        Assert.True(ReleaseVersion.TryParseTag("server-v0.3.0-alpha", "server-v", out var version));
        Assert.Equal("0.3.0-alpha", version.ToString());
        Assert.False(ReleaseVersion.TryParseTag("client-v0.3.0", "server-v", out _));
        Assert.False(ReleaseVersion.TryParseTag(null, "server-v", out _));
    }

    [Fact]
    public void Orders_releases_and_pre_releases()
    {
        Assert.True(V("0.2.1") < V("0.3.0-alpha"));
        Assert.True(V("0.3.0-alpha") < V("0.3.0"));
        Assert.True(V("0.3.0-alpha") < V("0.3.0-alpha.1"));
        Assert.True(V("0.3.0-alpha.2") < V("0.3.0-alpha.10"));
        Assert.True(V("0.3.0-alpha.1") < V("0.3.0-beta"));
        Assert.True(V("0.3.0-1") < V("0.3.0-alpha"));
        Assert.True(V("0.10.0") > V("0.9.0"));
        Assert.Equal(0, V("1.0.0+a").CompareTo(V("1.0.0+b")));
    }

    [Fact]
    public void Core_drops_the_label()
    {
        Assert.Equal("0.2.1", V("0.2.1-alpha").Core);
        Assert.True(V("0.2.1-alpha").IsPreRelease);
        Assert.False(V("0.2.1").IsPreRelease);
    }
}
