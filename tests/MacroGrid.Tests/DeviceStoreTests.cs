using MacroGrid.Core.Devices;
using MacroGrid.Core.Security;
using MacroGrid.Windows.Security;

namespace MacroGrid.Tests;

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
        store.Touch("device-1", "Phone (renamed)");

        var updated = store.FindByToken(device.Token)!;
        Assert.Equal("Phone (renamed)", updated.Name);
        Assert.True(updated.LastSeenAt > originalLastSeen);
    }

    /// <summary>Reversible and obviously not the plain token, so the tests can see what is on disk.</summary>
    private sealed class TestProtector(string key = "k1") : ISecretProtector
    {
        public string Protect(string secret) => $"{key}:" + new string(secret.Reverse().ToArray());
        public string? Unprotect(string protectedSecret) =>
            protectedSecret.StartsWith($"{key}:", StringComparison.Ordinal) ? new string(protectedSecret[(key.Length + 1)..].Reverse().ToArray()) : null;
    }

    private string DevicesFile => Path.Combine(_dir, "devices.json");

    [Fact]
    public void With_a_protector_the_token_is_not_written_in_plain_text()
    {
        var device = new DeviceStore(_dir, new TestProtector()).Pair("device-1", "Phone");

        var text = File.ReadAllText(DevicesFile);
        Assert.DoesNotContain(device.Token, text);
        Assert.Contains("protectedToken", text);
        Assert.Equal("device-1", new DeviceStore(_dir, new TestProtector()).FindByToken(device.Token)!.Id);
    }

    [Fact]
    public void A_plain_file_from_an_older_version_is_converted_on_load()
    {
        var device = new DeviceStore(_dir).Pair("device-1", "Phone");
        Assert.Contains(device.Token, File.ReadAllText(DevicesFile));

        var store = new DeviceStore(_dir, new TestProtector());

        Assert.Equal("device-1", store.FindByToken(device.Token)!.Id);
        Assert.DoesNotContain(device.Token, File.ReadAllText(DevicesFile));
        Assert.Equal(0, store.UnreadableOnLoad);
    }

    [Fact]
    public void A_token_that_cannot_be_decrypted_drops_only_that_device()
    {
        new DeviceStore(_dir, new TestProtector("other-pc")).Pair("device-1", "Phone");
        var store = new DeviceStore(_dir, new TestProtector());
        var kept = store.Pair("device-2", "Tablet");

        var reloaded = new DeviceStore(_dir, new TestProtector());

        Assert.Equal(1, store.UnreadableOnLoad);
        Assert.Single(reloaded.All);
        Assert.Equal("device-2", reloaded.FindByToken(kept.Token)!.Id);
    }

    [Fact]
    public void Other_fields_survive_the_round_trip()
    {
        var store = new DeviceStore(_dir, new TestProtector());
        store.Pair("device-1", "Phone");
        store.AssignProfile("device-1", "profile-7");
        store.SetFollowActiveWindow("device-1", true);

        var device = new DeviceStore(_dir, new TestProtector()).All.Single();

        Assert.Equal("profile-7", device.AssignedProfileId);
        Assert.True(device.FollowActiveWindow);
        Assert.Equal("Phone", device.Name);
    }

    [Fact]
    public void Dpapi_round_trips_for_this_user_and_rejects_garbage()
    {
        var protector = new DpapiSecretProtector();
        var blob = protector.Protect("secret-token");

        Assert.NotEqual("secret-token", blob);
        Assert.Equal("secret-token", protector.Unprotect(blob));
        Assert.Null(protector.Unprotect("not base64!"));
        Assert.Null(protector.Unprotect(Convert.ToBase64String(new byte[] { 1, 2, 3 })));
    }
}
