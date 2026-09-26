using MacroGrid.Core.Devices;

namespace MacroGrid.Tests;

public sealed class PairingServiceTests
{
    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private const string Phone = "192.168.1.20";
    private const string Attacker = "192.168.1.66";

    private static string WrongPin(string pin) => pin == "000000" ? "111111" : "000000";

    [Fact]
    public void Generated_pin_is_six_digits()
    {
        var pairing = new PairingService();

        Assert.Matches("^[0-9]{6}$", pairing.Open().Pin);
    }

    [Fact]
    public void Pairing_is_closed_until_the_window_opens_it()
    {
        var pairing = new PairingService();

        Assert.False(pairing.IsOpen);
        // Whatever PIN is sent, nothing is accepted while pairing is closed.
        Assert.Equal(PairingOutcome.Closed, pairing.TryPair("123456", Phone).Outcome);
    }

    [Fact]
    public void The_open_pin_is_accepted_once()
    {
        var pairing = new PairingService();
        var code = pairing.Open();

        Assert.Equal(PairingOutcome.Accepted, pairing.TryPair(code.Pin, Phone).Outcome);

        // Used up: the same PIN does not pair a second device, the window shows the new one.
        var next = pairing.Keep();
        if (next.Pin != code.Pin) Assert.NotEqual(PairingOutcome.Accepted, pairing.TryPair(code.Pin, Phone).Outcome);
        Assert.Equal(PairingOutcome.Accepted, pairing.TryPair(next.Pin, Phone).Outcome);
    }

    [Fact]
    public void No_pin_asks_for_one_and_is_not_counted()
    {
        var pairing = new PairingService();
        var code = pairing.Open();

        for (var i = 0; i < 50; i++) Assert.Equal(PairingOutcome.PinRequired, pairing.TryPair(null, Phone).Outcome);
        Assert.Equal(PairingOutcome.PinRequired, pairing.TryPair("", Phone).Outcome);

        Assert.Equal(PairingOutcome.Accepted, pairing.TryPair(code.Pin, Phone).Outcome);
    }

    [Fact]
    public void Pairing_closes_shortly_after_the_window_stops_polling()
    {
        var time = new ManualTime();
        var pairing = new PairingService(time);
        var code = pairing.Open();

        time.Advance(PairingService.Lease - TimeSpan.FromSeconds(1));
        Assert.True(pairing.IsOpen);

        time.Advance(TimeSpan.FromSeconds(2));
        Assert.False(pairing.IsOpen);
        Assert.Equal(PairingOutcome.Closed, pairing.TryPair(code.Pin, Phone).Outcome);
    }

    [Fact]
    public void Polling_keeps_the_same_pin_until_it_expires()
    {
        var time = new ManualTime();
        var pairing = new PairingService(time);
        var code = pairing.Open();

        for (var elapsed = TimeSpan.Zero; elapsed < PairingService.Ttl - TimeSpan.FromSeconds(10); elapsed += TimeSpan.FromSeconds(3))
        {
            time.Advance(TimeSpan.FromSeconds(3));
            Assert.Equal(code.Pin, pairing.Keep().Pin);
        }

        time.Advance(TimeSpan.FromSeconds(10));
        var renewed = pairing.Keep();
        Assert.Equal(time.GetUtcNow() + PairingService.Ttl, renewed.ExpiresAt);
        Assert.True(pairing.IsOpen);
    }

    [Fact]
    public void Polling_after_pairing_closed_starts_again_with_a_new_pin()
    {
        var time = new ManualTime();
        var pairing = new PairingService(time);
        pairing.Open();

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.False(pairing.IsOpen);
        var again = pairing.Keep();

        Assert.True(pairing.IsOpen);
        Assert.Equal(time.GetUtcNow() + PairingService.Ttl, again.ExpiresAt);
    }

    [Fact]
    public void An_address_is_blocked_after_repeated_wrong_pins_even_for_the_right_pin()
    {
        var time = new ManualTime();
        var pairing = new PairingService(time);
        var code = pairing.Open();

        for (var i = 1; i < PairingService.FailuresBeforeBlock; i++)
            Assert.Equal(PairingOutcome.WrongPin, pairing.TryPair(WrongPin(code.Pin), Attacker).Outcome);

        var blocked = pairing.TryPair(WrongPin(code.Pin), Attacker);
        Assert.Equal(PairingOutcome.Blocked, blocked.Outcome);
        Assert.True(blocked.NewlyBlocked);
        Assert.Equal(PairingService.FirstBlock, blocked.RetryAfter);

        var during = pairing.TryPair(code.Pin, Attacker);
        Assert.Equal(PairingOutcome.Blocked, during.Outcome);
        Assert.False(during.NewlyBlocked);

        // Another device is not affected.
        Assert.Equal(PairingOutcome.Accepted, pairing.TryPair(code.Pin, Phone).Outcome);
    }

    [Fact]
    public void The_block_ends_and_the_next_round_lasts_longer()
    {
        var time = new ManualTime();
        var pairing = new PairingService(time);
        pairing.Open();

        for (var i = 0; i < PairingService.FailuresBeforeBlock; i++) pairing.TryPair("999999x", Attacker);
        time.Advance(PairingService.FirstBlock + TimeSpan.FromSeconds(1));
        pairing.Keep();

        PairingResult last = default;
        for (var i = 0; i < PairingService.FailuresBeforeBlock; i++) last = pairing.TryPair("999999x", Attacker);

        Assert.Equal(PairingOutcome.Blocked, last.Outcome);
        Assert.Equal(PairingService.FirstBlock * 2, last.RetryAfter);
    }

    [Fact]
    public void Block_duration_doubles_up_to_the_maximum()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), PairingService.BlockDuration(1));
        Assert.Equal(TimeSpan.FromSeconds(60), PairingService.BlockDuration(2));
        Assert.Equal(TimeSpan.FromSeconds(120), PairingService.BlockDuration(3));
        Assert.Equal(PairingService.MaxBlock, PairingService.BlockDuration(10));
        Assert.Equal(PairingService.MaxBlock, PairingService.BlockDuration(1000));
    }

    [Fact]
    public void Too_many_wrong_pins_from_many_addresses_replace_the_pin()
    {
        var pairing = new PairingService();
        var code = pairing.Open();

        PairingResult last = default;
        for (var i = 0; i < PairingService.FailuresBeforeNewPin; i++)
            last = pairing.TryPair(WrongPin(code.Pin), $"10.0.0.{i + 1}");

        Assert.True(last.PinRenewed);
        var current = pairing.Keep();
        if (current.Pin != code.Pin) Assert.NotEqual(PairingOutcome.Accepted, pairing.TryPair(code.Pin, Phone).Outcome);
        Assert.Equal(PairingOutcome.Accepted, pairing.TryPair(current.Pin, Phone).Outcome);
    }

    [Fact]
    public void An_old_typo_is_forgotten_after_a_quiet_while()
    {
        var time = new ManualTime();
        var pairing = new PairingService(time);
        var code = pairing.Open();
        for (var i = 1; i < PairingService.FailuresBeforeBlock; i++) pairing.TryPair(WrongPin(code.Pin), Phone);

        time.Advance(PairingService.MaxBlock + TimeSpan.FromMinutes(1));
        code = pairing.Keep();

        // Would be the fifth failure in a row, but the old ones no longer count.
        Assert.Equal(PairingOutcome.WrongPin, pairing.TryPair(WrongPin(code.Pin), Phone).Outcome);
    }

    [Fact]
    public void The_address_table_stays_bounded()
    {
        var pairing = new PairingService();
        pairing.Open();

        for (var i = 0; i < PairingService.MaxTrackedAddresses * 4; i++)
            pairing.TryPair("not-a-pin", $"10.{i / 65536}.{i / 256 % 256}.{i % 256}");

        Assert.True(pairing.TrackedAddressCount <= PairingService.MaxTrackedAddresses);
    }
}
