using MacroStation.Core.Devices;

namespace MacroStation.Tests;

public sealed class DeviceStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ms-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void Pair_issues_a_token_findable_afterwards()
    {
        var store = new DeviceStore(_dir);

        var device = store.Pair("device-1", "Telefon");

        Assert.NotEmpty(device.Token);
        Assert.Equal(device.Id, store.FindByToken(device.Token)!.Id);
    }

    [Fact]
    public void FindByToken_returns_null_for_unknown_or_empty_token()
    {
        var store = new DeviceStore(_dir);
        store.Pair("device-1", "Telefon");

        Assert.Null(store.FindByToken("not-a-real-token"));
        Assert.Null(store.FindByToken(null));
        Assert.Null(store.FindByToken(""));
    }

    [Fact]
    public void Pairing_survives_reload()
    {
        var device = new DeviceStore(_dir).Pair("device-1", "Telefon");

        var reloaded = new DeviceStore(_dir).FindByToken(device.Token);

        Assert.NotNull(reloaded);
        Assert.Equal("Telefon", reloaded!.Name);
    }

    [Fact]
    public void Re_pairing_the_same_device_id_issues_a_new_token_and_invalidates_the_old_one()
    {
        var store = new DeviceStore(_dir);
        var first = store.Pair("device-1", "Telefon");

        var second = store.Pair("device-1", "Telefon (yeniden kuruldu)");

        Assert.NotEqual(first.Token, second.Token);
        Assert.Null(store.FindByToken(first.Token));
        Assert.NotNull(store.FindByToken(second.Token));
    }

    [Fact]
    public void Revoke_removes_the_device()
    {
        var store = new DeviceStore(_dir);
        var device = store.Pair("device-1", "Telefon");

        Assert.True(store.Revoke(device.Id));
        Assert.Null(store.FindByToken(device.Token));
        Assert.False(store.Revoke(device.Id));
    }

    [Fact]
    public void Touch_updates_last_seen_and_name_without_changing_the_token()
    {
        var store = new DeviceStore(_dir);
        var device = store.Pair("device-1", "Telefon");
        var originalLastSeen = device.LastSeenAt;

        Thread.Sleep(10);
        store.Touch("device-1", "Telefon (yeniden adlandırıldı)");

        var updated = store.FindByToken(device.Token)!;
        Assert.Equal("Telefon (yeniden adlandırıldı)", updated.Name);
        Assert.True(updated.LastSeenAt > originalLastSeen);
    }
}
