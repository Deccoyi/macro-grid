using MacroGrid.Core.Devices;

namespace MacroGrid.Tests;

public class HostGuardTests
{
    [Theory]
    [InlineData("localhost:9820", true)]
    [InlineData("LOCALHOST:9820", true)]
    [InlineData("127.0.0.1:9820", true)]
    [InlineData("[::1]:9820", true)]
    [InlineData("localhost:9821", true)]
    [InlineData("localhost:5190", true)]
    [InlineData("localhost:5192", true)]
    [InlineData("localhost:1234", false)]
    [InlineData("localhost", false)]
    [InlineData("evil.example:9820", false)]
    [InlineData("localhost.evil.example:9820", false)]
    [InlineData("192.168.1.20:9820", false)]
    [InlineData("[::1]", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_loopback_names_with_the_servers_ports_are_allowed(string? host, bool expected)
    {
        Assert.Equal(expected, HostGuard.IsAllowed(host));
    }
}

public class WebSocketOriginGuardTests
{
    [Theory]
    [InlineData(null, "192.168.1.20:9820", true)]
    [InlineData("https://localhost", "192.168.1.20:9820", true)]
    [InlineData("http://localhost", "192.168.1.20:9820", true)]
    [InlineData("http://192.168.1.20:9820", "192.168.1.20:9820", true)]
    [InlineData("https://pc.local:9821", "pc.local:9821", true)]
    [InlineData("https://evil.example", "192.168.1.20:9820", false)]
    [InlineData("http://192.168.1.20:9820", "192.168.1.30:9820", false)]
    [InlineData("null", "192.168.1.20:9820", false)]
    [InlineData("http://evil.example", null, false)]
    public void A_browser_origin_must_be_the_deck_or_the_phone_app(string? origin, string? host, bool expected)
    {
        Assert.Equal(expected, WebSocketOriginGuard.IsAllowed(origin, host));
    }
}
