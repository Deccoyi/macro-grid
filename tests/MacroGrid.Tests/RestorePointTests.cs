using MacroGrid.Core.Backup;

namespace MacroGrid.Tests;

public sealed class RestorePointTests : IDisposable
{
    private readonly BackupTestWorld _world = new();

    public void Dispose() => _world.Dispose();

    private string[] Files() => Directory.Exists(_world.RestorePointsFolder) ? Directory.GetFiles(_world.RestorePointsFolder, "*.mgbackup") : [];

    [Fact]
    public void A_point_holds_the_data_and_is_listed_with_its_reason()
    {
        _world.AddProfile("One");

        _world.Service.CreateRestorePoint(BackupService.ReasonDelete);

        var point = Assert.Single(_world.Service.ListRestorePoints());
        Assert.Equal("delete", point.Reason);
        var read = BackupFile.Read(_world.Service.ReadRestorePoint(point.FileName));
        Assert.Equal(BackupFile.KindRestorePoint, read.Manifest.Kind);
        Assert.Contains(read.Content.Profiles, p => p.Name == "One");
    }

    [Fact]
    public void The_same_data_writes_no_second_file_but_changed_data_does()
    {
        _world.Service.CreateRestorePoint(BackupService.ReasonImport);
        _world.Service.CreateRestorePoint(BackupService.ReasonDelete);
        Assert.Single(Files());

        _world.AddProfile("Changed");
        _world.Service.CreateRestorePoint(BackupService.ReasonDelete);

        Assert.Equal(2, Files().Length);
    }

    [Fact]
    public void An_unknown_reason_is_refused()
    {
        Assert.Throws<BackupException>(() => _world.Service.CreateRestorePoint("whatever"));
        Assert.False(BackupService.IsKnownReason("../x"));
    }

    [Fact]
    public void A_folder_that_cannot_be_written_is_an_error()
    {
        File.WriteAllText(_world.RestorePointsFolder, "a file where the folder should be");

        Assert.Throws<BackupException>(() => _world.Service.CreateRestorePoint(BackupService.ReasonRestore));
    }

    [Fact]
    public void Only_the_newest_ten_are_kept()
    {
        for (var i = 0; i < 14; i++)
        {
            _world.AddProfile("Profile " + i);
            _world.Service.CreateRestorePoint(BackupService.ReasonDelete);
        }

        var points = _world.Service.ListRestorePoints();

        Assert.Equal(BackupService.KeepNewest, points.Count);
        Assert.Equal(BackupService.KeepNewest, Files().Length);
        // The newest point holds all fourteen profiles (and the default one), the oldest kept one holds fewer.
        Assert.Equal(points.OrderByDescending(p => p.CreatedAt).First().FileName, points[0].FileName);
        var newest = BackupFile.Read(_world.Service.ReadRestorePoint(points[0].FileName));
        Assert.True(newest.Content.Profiles.Count >= 14);
    }

    [Fact]
    public void A_name_outside_the_list_is_refused()
    {
        _world.Service.CreateRestorePoint(BackupService.ReasonImport);

        Assert.Throws<BackupException>(() => _world.Service.ReadRestorePoint("../preferences.json"));
        Assert.Throws<BackupException>(() => _world.Service.ReadRestorePoint("nope.mgbackup"));
    }

    [Fact]
    public void A_version_change_makes_one_point_and_the_same_version_none()
    {
        Assert.False(_world.Service.TryCreateUpdatePoint(null, "2.0.0"));
        Assert.False(_world.Service.TryCreateUpdatePoint("2.0.0", "2.0.0"));
        Assert.Empty(Files());

        Assert.True(_world.Service.TryCreateUpdatePoint("1.0.0", "2.0.0"));

        Assert.Equal("update", Assert.Single(_world.Service.ListRestorePoints()).Reason);
    }

    [Fact]
    public void A_failed_update_point_does_not_throw()
    {
        File.WriteAllText(_world.RestorePointsFolder, "blocked");

        Assert.False(_world.Service.TryCreateUpdatePoint("1.0.0", "2.0.0"));
    }
}
