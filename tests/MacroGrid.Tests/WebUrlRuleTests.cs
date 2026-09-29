using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Core.Web;

namespace MacroGrid.Tests;

public class WebUrlRuleTests
{
    [Theory]
    [InlineData("https://example.org/chat?channel=abc&token=1")]
    [InlineData("http://192.0.2.50:8080/panel")]
    [InlineData("https://example.org:8443/")]
    public void Web_pages_are_allowed(string url) => Assert.True(WebUrlRule.IsAllowed(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.org/chat")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("blob:https://example.org/1")]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("ftp://example.org/file")]
    [InlineData("https://user:pass@example.org/")]
    [InlineData("https://user@example.org/")]
    [InlineData(" https://example.org/")]
    public void Other_schemes_and_credentials_are_refused(string? url) => Assert.False(WebUrlRule.IsAllowed(url));

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("https://localhost:9821/")]
    [InlineData("https://LOCALHOST/")]
    [InlineData("https://localhost./")]
    [InlineData("https://app.localhost/")]
    [InlineData("http://127.0.0.1:9820/")]
    [InlineData("http://127.5.5.5/")]
    [InlineData("http://0x7f.1/")]
    [InlineData("http://2130706433/")]
    [InlineData("http://0177.0.0.1/")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::]/")]
    [InlineData("http://[::ffff:127.0.0.1]/")]
    public void Loopback_in_every_form_is_refused(string url) => Assert.False(WebUrlRule.IsAllowed(url));

    [Fact]
    public void The_machine_itself_is_refused() =>
        Assert.False(WebUrlRule.IsAllowed("http://" + Environment.MachineName + ":9820/"));

    [Fact]
    public void An_address_over_the_length_cap_is_refused() =>
        Assert.False(WebUrlRule.IsAllowed("https://example.org/" + new string('a', WebUrlRule.MaxLength)));

    [Fact]
    public void The_host_for_the_log_never_carries_the_query()
    {
        Assert.True(WebUrlRule.TryGetHost("https://Example.org/chat?token=secret", out var host));
        Assert.Equal("example.org", host);
        Assert.DoesNotContain("secret", WebUrlRule.HostForLog("https://example.org/chat?token=secret"));
    }

    private static Widget WebWidget(string url, string type = WidgetTypes.Web) =>
        new() { Type = type, Props = new JsonObject { ["url"] = url } };

    [Fact]
    public void Sanitize_clears_only_refused_web_addresses()
    {
        var good = WebWidget("https://example.org/chat");
        var bad = WebWidget("javascript:alert(1)");
        var loop = WebWidget("http://localhost:9820/");
        var notWeb = WebWidget("javascript:alert(1)", WidgetTypes.Image);
        var profile = new Profile { Name = "P", Pages = [new Page { Name = "1", Widgets = [good, bad, loop, notWeb] }] };

        var dropped = WebUrlRule.Sanitize(profile);

        Assert.Equal(2, dropped.Count);
        Assert.Equal("https://example.org/chat", good.Props!["url"]!.GetValue<string>());
        Assert.False(bad.Props!.ContainsKey("url"));
        Assert.False(loop.Props!.ContainsKey("url"));
        Assert.True(notWeb.Props!.ContainsKey("url"));
    }
}
