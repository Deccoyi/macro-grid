using MacroGrid.Core.Devices;

namespace MacroGrid.Tests;

public class OriginGuardTests
{
    [Theory]
    [InlineData("http://localhost:9820", true)]
    [InlineData("http://127.0.0.1:9820", true)]
    [InlineData("http://[::1]:9820", true)]
    [InlineData("http://localhost:5190", true)]
    [InlineData("http://localhost:5192", true)]
    [InlineData("http://localhost:1234", false)]
    [InlineData("https://localhost:9820", false)]
    [InlineData("http://evil.example", false)]
    [InlineData("null", false)]
    public void Only_the_editors_own_origins_are_allowed(string origin, bool expected)
    {
        Assert.Equal(expected, OriginGuard.IsAllowed(origin));
    }

    [Fact]
    public void A_request_with_no_Origin_header_is_allowed()
    {
        // Only a browser sends Origin, and not for every request (e.g. a same-origin GET) — curl, the phone
        // app's own client and same-origin GETs must keep working.
        Assert.True(OriginGuard.IsAllowed(null));
    }
}
