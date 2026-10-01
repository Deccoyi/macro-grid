using System.Text.Json.Nodes;
using MacroGrid.Core.Backup;
using MacroGrid.Core.Model;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Tests;

public sealed class BackupRestorerTests : IDisposable
{
    private readonly BackupTestWorld _world = new();

    public void Dispose() => _world.Dispose();

    private InspectResult InspectOf(BackupContent content) =>
        _world.Restorer.Inspect(BackupFile.Write(content, BackupFile.KindBackup, "manual", "1.0.0", []));

    private static Profile Backed(string id, string name) => new()
    {
        Id = id,
        Name = name,
        Pages = [new Page { Id = "a", Name = "Main", Widgets = [new Widget { Id = "w1", Text = name }] }],
    };

    private static RestoreRequestItem Pick(RestoreItem item) => new(item.Kind, item.Key);

    [Fact]
    public void Items_are_new_different_or_same()
    {
        var known = _world.AddProfile("Known");
        var content = new BackupContent { Profiles = [Backed(known.Id, "Renamed"), Backed("fresh", "Fresh")] };

        var result = InspectOf(content);

        Assert.Equal(RestoreState.Different, result.Items.Single(i => i.Key == known.Id).State);
        Assert.Equal(RestoreState.New, result.Items.Single(i => i.Key == "fresh").State);
    }

    [Fact]
    public void A_backup_of_the_current_data_is_all_same()
    {
        _world.AddProfile("One");

        var result = _world.Restorer.Inspect(_world.Service.BuildBackup());

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, i => Assert.Equal(RestoreState.Same, i.State));
    }

    [Fact]
    public async Task Restore_applies_only_the_ticked_items_and_makes_a_restore_point_first()
    {
        var one = _world.AddProfile("One");
        var result = InspectOf(new BackupContent { Profiles = [Backed(one.Id, "Back"), Backed("fresh", "Fresh")] });

        var done = await _world.Restorer.RestoreAsync(result.Id, [Pick(result.Items.Single(i => i.Key == "fresh"))]);

        Assert.True(Assert.Single(done).Ok);
        Assert.Equal("Fresh", _world.Profiles.Get("fresh")!.Name);
        Assert.Equal("One", _world.Profiles.Get(one.Id)!.Name);
        Assert.Contains("fresh", _world.Broadcast);
        var point = Assert.Single(_world.Service.ListRestorePoints());
        Assert.Equal("restore", point.Reason);
        var before = BackupFile.Read(_world.Service.ReadRestorePoint(point.FileName));
        Assert.DoesNotContain(before.Content.Profiles, p => p.Id == "fresh");
    }

    [Fact]
    public async Task Nothing_is_deleted_by_a_restore()
    {
        var one = _world.AddProfile("One");
        var result = InspectOf(new BackupContent { Profiles = [Backed("fresh", "Fresh")] });

        await _world.Restorer.RestoreAsync(result.Id, [Pick(result.Items.Single())]);

        Assert.NotNull(_world.Profiles.Get(one.Id));
    }

    [Fact]
    public async Task A_restore_that_cannot_make_its_point_applies_nothing()
    {
        File.WriteAllText(_world.RestorePointsFolder, "blocked");
        var result = InspectOf(new BackupContent { Profiles = [Backed("fresh", "Fresh")] });

        await Assert.ThrowsAsync<BackupException>(() => _world.Restorer.RestoreAsync(result.Id, [Pick(result.Items.Single())]));

        Assert.Null(_world.Profiles.Get("fresh"));
    }

    [Fact]
    public async Task An_unknown_session_is_refused_and_a_second_inspect_replaces_the_first()
    {
        var first = InspectOf(new BackupContent { Profiles = [Backed("p1", "A")] });
        var second = InspectOf(new BackupContent { Profiles = [Backed("p2", "B")] });

        await Assert.ThrowsAsync<RestoreSessionException>(() => _world.Restorer.RestoreAsync(first.Id, []));
        await Assert.ThrowsAsync<RestoreSessionException>(() => _world.Restorer.RestoreAsync("nope", []));
        Assert.Empty(await _world.Restorer.RestoreAsync(second.Id, []));
    }

    [Fact]
    public async Task Preferences_never_switch_unencrypted_connections_on()
    {
        _world.Preferences.Save(new AppPreferences { AllowUnencrypted = false });
        var result = InspectOf(new BackupContent { Preferences = new AppPreferences { AllowUnencrypted = true, Theme = "light" } });

        var done = await _world.Restorer.RestoreAsync(result.Id, [Pick(result.Items.Single(i => i.Kind == RestoreItemKind.Preferences))]);

        Assert.True(Assert.Single(done).Ok);
        Assert.False(_world.Preferences.Get().AllowUnencrypted);
        Assert.Equal("light", _world.Preferences.Get().Theme);
        Assert.Contains(done[0].Warnings, w => w.Code == "unencryptedKept");
    }

    [Fact]
    public async Task Variables_are_merged_and_a_type_conflict_is_skipped()
    {
        _world.Variables.TryReplace([new UserVariable("score", VariableType.Number, 1.0, true, null)], out _);
        var result = InspectOf(new BackupContent
        {
            Variables = [new UserVariable("score", VariableType.Text, "x", true, null), new UserVariable("extra", VariableType.Number, 2.0, true, null)],
        });

        var item = result.Items.Single(i => i.Kind == RestoreItemKind.Variables);
        Assert.Equal(1, item.Added);
        Assert.Equal(1, item.Skipped);
        await _world.Restorer.RestoreAsync(result.Id, [Pick(item)]);

        var all = _world.Variables.List();
        Assert.Contains(all, v => v.Name == "extra");
        Assert.Equal(VariableType.Number, all.Single(v => v.Name == "score").Type);
    }

    [Fact]
    public async Task A_device_paired_here_takes_its_choices_and_a_missing_profile_becomes_none()
    {
        _world.Devices.Pair("dev1", "Phone");
        var result = InspectOf(new BackupContent
        {
            Devices = [new BackupDevice("dev1", "Phone", "gone", true, true), new BackupDevice("other", "Tablet", null, false, false)],
        });

        Assert.Contains(result.Warnings, w => w.Code == "deviceNotPaired");
        var done = await _world.Restorer.RestoreAsync(result.Id, [Pick(result.Items.Single(i => i.Kind == RestoreItemKind.Device))]);

        Assert.True(Assert.Single(done).Ok);
        Assert.Contains(done[0].Warnings, w => w.Code == "deviceProfileMissing");
        var device = _world.Devices.All.Single(d => d.Id == "dev1");
        Assert.Null(device.AssignedProfileId);
        Assert.True(device.FollowActiveWindow);
        Assert.True(device.AutoSwitchLocked);
    }

    [Fact]
    public void Plugin_settings_of_a_plugin_that_is_not_running_are_a_warning_not_an_item()
    {
        var content = new BackupContent();
        content.PluginSettings["obs"] = new JsonObject { ["host"] = "x" };

        var result = InspectOf(content);

        Assert.Empty(result.Items);
        Assert.Contains(result.Warnings, w => w.Code == "pluginSettingsNotRunning");
    }

    [Fact]
    public async Task A_language_pack_goes_through_its_store_and_a_bad_one_is_that_items_error()
    {
        var result = InspectOf(new BackupContent { LanguagePacks = { ["xx"] = "{\"meta\":{\"tag\":\"xx\"}}" } });

        var done = await _world.Restorer.RestoreAsync(result.Id, [Pick(result.Items.Single())]);

        Assert.Single(done);
        Assert.Equal(done[0].Ok, _world.Languages.Read("xx") is not null);
    }
}
