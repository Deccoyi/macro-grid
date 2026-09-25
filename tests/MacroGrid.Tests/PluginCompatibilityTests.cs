using MacroGrid.Core.Plugins;
using MacroGrid.Core.Sessions;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginCompatibilityTests
{
    [Fact]
    public void The_server_and_the_sdk_report_one_three_part_version()
    {
        Assert.Equal(PluginSdk.Version, ClientHub.ServerVersion);
        Assert.True(SemVer.IsThreePartVersion(PluginSdk.Version), PluginSdk.Version);
    }

    [Fact]
    public void A_macro_grid_field_at_or_below_the_running_version_is_compatible()
    {
        Assert.True(PluginCompatibility.Check("1.3.2", "1.3.0", null).Compatible);
        Assert.True(PluginCompatibility.Check("1.3.2", "1.0.0", null).Compatible);
    }

    [Fact]
    public void A_newer_macro_grid_field_says_which_version_is_needed()
    {
        var result = PluginCompatibility.Check("1.2.4", "1.3.0", null);

        Assert.False(result.Compatible);
        Assert.Equal("Needs Macro Grid 1.3.0 or newer, this is 1.2.4", result.Reason);
    }

    [Fact]
    public void A_different_major_says_to_rebuild()
    {
        var result = PluginCompatibility.Check("2.0.0", "1.3.0", null);

        Assert.False(result.Compatible);
        Assert.Contains("must be rebuilt", result.Reason);
    }

    [Fact]
    public void The_macro_grid_field_wins_over_legacy_fields()
    {
        Assert.True(PluginCompatibility.Check("1.0.0", "1.0.0", "^0.3.0").Compatible);
    }

    [Theory]
    [InlineData("^0.4.0")]
    [InlineData("^0.4.7")]
    [InlineData("0.4.0")]
    public void A_legacy_sdk_0_4_range_counts_as_1_0_0(string sdkVersion)
    {
        Assert.Equal("1.0.0", PluginCompatibility.Required(null, sdkVersion, out _));
        Assert.True(PluginCompatibility.Check("1.2.0", null, sdkVersion).Compatible);
        Assert.False(PluginCompatibility.Check("2.0.0", null, sdkVersion).Compatible);
    }

    [Theory]
    [InlineData("^0.3.0")]
    [InlineData("^0.3.1")]
    [InlineData("^0.2.0")]
    [InlineData("^0.5.0")]
    [InlineData("^1.0.0")]
    public void Any_other_legacy_sdk_range_must_be_rebuilt(string sdkVersion)
    {
        var result = PluginCompatibility.Check("1.0.0", null, sdkVersion);

        Assert.False(result.Compatible);
        Assert.Contains("rebuilt for Macro Grid 1.0.0", result.Reason);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("1.0", null)]
    [InlineData("v1.0.0", null)]
    public void A_missing_or_malformed_version_is_incompatible_with_a_reason(string? macroGrid, string? sdkVersion)
    {
        var result = PluginCompatibility.Check("1.0.0", macroGrid, sdkVersion);

        Assert.False(result.Compatible);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
}
