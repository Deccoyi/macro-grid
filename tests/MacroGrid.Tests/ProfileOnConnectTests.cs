using MacroGrid.Core.Devices;
using MacroGrid.Core.Model;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Profiles;

namespace MacroGrid.Tests;

public sealed class ProfileOnConnectTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    private static Profile Add(ProfileStore profiles, string name)
    {
        var profile = new Profile { Name = name, Pages = [new Page { Name = "Page 1", Cols = 4, Rows = 3 }] };
        profiles.Save(profile);
        return profile;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void A_profile_picked_on_the_device_is_opened_again_on_connect_and_survives_a_restart()
    {
        var profiles = new ProfileStore(_dir);
        var second = Add(profiles, "Second");
        var device = new DeviceStore(_dir).Pair("device-1", "Telefon");

        Assert.Equal(profiles.First().Id, ProfileResolver.ResolveOnConnect(device, profiles, new PreferencesStore(_dir)).Id);

        new DeviceStore(_dir).SetLastProfile(device.Id, second.Id);
        var reloaded = new DeviceStore(_dir).All.Single();

        Assert.Equal(second.Id, ProfileResolver.ResolveOnConnect(reloaded, profiles, new PreferencesStore(_dir)).Id);
    }

    [Fact]
    public void A_last_pick_that_was_deleted_falls_back_to_the_assignment_or_default()
    {
        var profiles = new ProfileStore(_dir);
        var devices = new DeviceStore(_dir);
        var device = devices.Pair("device-1", "Telefon");
        devices.SetLastProfile(device.Id, "no-such-profile");

        Assert.Equal(profiles.First().Id, ProfileResolver.ResolveOnConnect(device, profiles, new PreferencesStore(_dir)).Id);
    }

    [Fact]
    public void Assigning_a_profile_in_the_editor_replaces_an_earlier_pick_on_the_device()
    {
        var profiles = new ProfileStore(_dir);
        var other = Add(profiles, "Other");
        var assigned = Add(profiles, "Assigned");
        var devices = new DeviceStore(_dir);
        var device = devices.Pair("device-1", "Telefon");
        devices.SetLastProfile(device.Id, other.Id);

        devices.AssignProfile(device.Id, assigned.Id);

        Assert.Null(device.LastProfileId);
        Assert.Equal(assigned.Id, ProfileResolver.ResolveOnConnect(device, profiles, new PreferencesStore(_dir)).Id);
    }
}
