using System.Net;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Preferences;

namespace MacroGrid.Tests;

public class PlainConnectionPolicyTests
{
    private static readonly IPAddress Lan = IPAddress.Parse("192.168.1.20");

    [Fact]
    public void By_default_the_plain_port_answers_everyone()
    {
        Assert.True(new AppPreferences().AllowUnencrypted);
        Assert.True(PlainConnectionPolicy.IsAllowed(true, 9820, 9820, Lan));
    }

    [Fact]
    public void Switched_off_the_plain_port_answers_only_this_computer()
    {
        Assert.False(PlainConnectionPolicy.IsAllowed(false, 9820, 9820, Lan));
        Assert.False(PlainConnectionPolicy.IsAllowed(false, 9820, 9820, null));
        Assert.True(PlainConnectionPolicy.IsAllowed(false, 9820, 9820, IPAddress.Loopback));
        Assert.True(PlainConnectionPolicy.IsAllowed(false, 9820, 9820, IPAddress.IPv6Loopback));
    }

    [Fact]
    public void Switched_off_the_encrypted_port_is_not_affected()
    {
        Assert.True(PlainConnectionPolicy.IsAllowed(false, 9821, 9820, Lan));
    }
}
