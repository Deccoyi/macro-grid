using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Backup;

namespace MacroGrid.Tests;

public sealed class BackupSafetyTests
{
    private sealed class ThrowingCollector(BackupTestWorld w, Exception error) : BackupCollector(w.Profiles, w.Tree, w.Preferences, w.Variables, w.Automation, w.Devices, w.Plugins, w.Languages)
    {
        public override BackupContent Collect() => throw error;
    }

    public static IEnumerable<object[]> Failures() =>
    [
        [new JsonException("bad")],
        [new CryptographicException("bad")],
        [new InvalidCastException("bad")],
    ];

    [Theory]
    [MemberData(nameof(Failures))]
    public void The_update_restore_point_never_stops_the_start(Exception error)
    {
        using var world = new BackupTestWorld();
        var service = new BackupService(new ThrowingCollector(world, error), world.Root, "1.0.0");

        Assert.False(service.TryCreateUpdatePoint("0.9.0", "1.0.0"));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void A_restore_point_that_cannot_be_collected_is_a_BackupException(Exception error)
    {
        using var world = new BackupTestWorld();
        var service = new BackupService(new ThrowingCollector(world, error), world.Root, "1.0.0");

        Assert.Throws<BackupException>(() => service.CreateRestorePoint(BackupService.ReasonDelete));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public void A_plugin_whose_settings_cannot_be_read_is_left_out_with_a_warning(Exception error)
    {
        var warnings = new List<string>();

        Assert.Null(BackupCollector.TryReadSettings("obs", () => throw error, warnings));
        Assert.Contains("obs", Assert.Single(warnings));
        Assert.NotNull(BackupCollector.TryReadSettings("ok", () => new JsonObject(), warnings));
        Assert.Single(warnings);
    }
}
