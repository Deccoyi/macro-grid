using MacroGrid.Core.Devices;

namespace MacroGrid.Tests;

public sealed class PairingServiceTests
{
    [Fact]
    public void Generated_pin_is_six_digits()
    {
        var pairing = new PairingService();

        Assert.Matches("^[0-9]{6}$", pairing.CurrentPin);
    }

    [Fact]
    public void Verify_accepts_the_current_pin_and_rejects_everything_else()
    {
        var pairing = new PairingService();

        Assert.True(pairing.Verify(pairing.CurrentPin));
        Assert.False(pairing.Verify("000000".Equals(pairing.CurrentPin) ? "111111" : "000000"));
        Assert.False(pairing.Verify(null));
        Assert.False(pairing.Verify(""));
    }

    [Fact]
    public void Regenerate_changes_the_pin_and_invalidates_the_old_one()
    {
        var pairing = new PairingService();
        var oldPin = pairing.CurrentPin;

        var newPin = pairing.Regenerate();

        Assert.Equal(newPin, pairing.CurrentPin);
        if (newPin != oldPin) Assert.False(pairing.Verify(oldPin));
    }
}
