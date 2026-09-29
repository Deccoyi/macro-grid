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
    public void A_min_macro_grid_field_at_or_below_the_running_version_is_compatible()
    {
        Assert.True(PluginCompatibility.Check("1.3.2", "1.3.0", null, null).Compatible);
        Assert.True(PluginCompatibility.Check("1.3.2", "1.0.0", null, null).Compatible);
    }

    [Fact]
    public void A_newer_min_macro_grid_field_says_which_version_is_needed()
    {
        var result = PluginCompatibility.Check("1.2.4", "1.3.0", null, null);

        Assert.False(result.Compatible);
        Assert.Equal("Needs Macro Grid editor 1.3.0 or newer, this is 1.2.4", result.Reason);
    }

    [Fact]
    public void A_different_major_says_to_rebuild()
    {
        var result = PluginCompatibility.Check("2.0.0", "1.3.0", null, null);

        Assert.False(result.Compatible);
        Assert.Contains("must be rebuilt", result.Reason);
    }

    [Fact]
    public void The_min_macro_grid_field_wins_over_legacy_fields()
    {
        Assert.True(PluginCompatibility.Check("1.0.0", "1.0.0", null, "^0.3.0").Compatible);
    }

    [Theory]
    [InlineData("^0.4.0")]
    [InlineData("^0.4.7")]
    [InlineData("0.4.0")]
    public void A_legacy_sdk_0_4_range_counts_as_1_0_0(string sdkVersion)
    {
        Assert.Equal("1.0.0", PluginCompatibility.Required(null, null, sdkVersion, out _));
        Assert.True(PluginCompatibility.Check("1.2.0", null, null, sdkVersion).Compatible);
        Assert.False(PluginCompatibility.Check("2.0.0", null, null, sdkVersion).Compatible);
    }

    [Theory]
    [InlineData("^0.3.0")]
    [InlineData("^0.3.1")]
    [InlineData("^0.2.0")]
    [InlineData("^0.5.0")]
    [InlineData("^1.0.0")]
    public void Any_other_legacy_sdk_range_must_be_rebuilt(string sdkVersion)
    {
        var result = PluginCompatibility.Check("1.0.0", null, null, sdkVersion);

        Assert.False(result.Compatible);
        Assert.Contains("rebuilt for Macro Grid editor 1.0.0", result.Reason);
    }

    [Fact]
    public void A_legacy_macro_grid_field_is_read_as_min_macro_grid()
    {
        Assert.Equal("1.3.0", PluginCompatibility.Required(null, "1.3.0", null, out _));
        Assert.True(PluginCompatibility.Check("1.3.2", null, "1.3.0", null).Compatible);
        Assert.Equal("Needs Macro Grid editor 1.3.0 or newer, this is 1.2.4", PluginCompatibility.Check("1.2.4", null, "1.3.0", null).Reason);
        Assert.False(PluginCompatibility.Check("2.0.0", null, "1.3.0", null).Compatible);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void The_legacy_macro_grid_field_is_read_only_when_min_macro_grid_is_absent(string? minMacroGrid)
    {
        Assert.Equal("1.1.0", PluginCompatibility.Declared(minMacroGrid, "1.1.0"));
    }

    [Fact]
    public void Min_macro_grid_wins_over_the_legacy_macro_grid_field()
    {
        Assert.Equal("1.3.0", PluginCompatibility.Required("1.3.0", "1.0.0", "^0.4.0", out _));
        Assert.False(PluginCompatibility.Check("1.2.0", "1.3.0", "1.0.0", null).Compatible);
        Assert.True(PluginCompatibility.Check("1.2.0", "1.0.0", "1.3.0", null).Compatible);
    }

    [Fact]
    public void A_malformed_legacy_macro_grid_field_is_named_in_the_reason()
    {
        var result = PluginCompatibility.Check("1.0.0", null, "1.0", null);

        Assert.False(result.Compatible);
        Assert.StartsWith("macroGrid must be", result.Reason);
    }

    [Fact]
    public void With_neither_field_nor_a_legacy_sdk_version_the_manifest_is_incompatible()
    {
        var result = PluginCompatibility.Check("1.0.0", null, null, null);

        Assert.False(result.Compatible);
        Assert.Equal("The manifest has no minMacroGrid field", result.Reason);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("1.0", null, null)]
    [InlineData("v1.0.0", null, null)]
    [InlineData("v1.0.0", "1.0.0", null)]
    public void A_missing_or_malformed_version_is_incompatible_with_a_reason(string? minMacroGrid, string? macroGrid, string? sdkVersion)
    {
        var result = PluginCompatibility.Check("1.0.0", minMacroGrid, macroGrid, sdkVersion);

        Assert.False(result.Compatible);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }
}
