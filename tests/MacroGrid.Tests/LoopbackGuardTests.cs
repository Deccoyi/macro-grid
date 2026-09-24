using System.Net;
using MacroGrid.Core.Devices;

namespace MacroGrid.Tests;

public class LoopbackGuardTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("::ffff:127.0.0.1", true)]
    [InlineData("192.168.1.20", false)]
    [InlineData("10.0.0.5", false)]
    [InlineData("::ffff:192.168.1.20", false)]
    public void Only_this_computer_counts_as_local(string address, bool expected)
    {
        Assert.Equal(expected, LoopbackGuard.IsLoopback(IPAddress.Parse(address)));
    }

    [Fact]
    public void An_unknown_address_is_not_trusted()
    {
        Assert.False(LoopbackGuard.IsLoopback(null));
    }
}
