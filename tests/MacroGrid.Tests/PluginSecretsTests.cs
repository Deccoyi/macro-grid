using MacroGrid.Core.Plugins;
using MacroGrid.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace MacroGrid.Tests;

public sealed class PluginSecretsTests
{
    /// <summary>Reversible and obviously not the plain secret, so the test can see the round trip actually goes through the protector.</summary>
    private sealed class TestProtector : ISecretProtector
    {
        public string Protect(string secret) => "p:" + new string(secret.Reverse().ToArray());
        public string? Unprotect(string protectedSecret) =>
            protectedSecret.StartsWith("p:", StringComparison.Ordinal) ? new string(protectedSecret[2..].Reverse().ToArray()) : null;
    }

    [Fact]
    public void Secrets_round_trips_through_the_configured_protector()
    {
        var host = new PluginHostCollector("1.0.0", Path.GetTempPath(), "t", new PluginStatusRegistry(), NullLogger.Instance, new TestProtector());

        var protectedValue = host.Secrets.Protect("hunter2");

        Assert.NotEqual("hunter2", protectedValue);
        Assert.Equal("hunter2", host.Secrets.Unprotect(protectedValue));
    }

    [Fact]
    public void Unprotect_returns_null_for_a_value_the_protector_does_not_recognize()
    {
        var host = new PluginHostCollector("1.0.0", Path.GetTempPath(), "t", new PluginStatusRegistry(), NullLogger.Instance, new TestProtector());

        Assert.Null(host.Secrets.Unprotect("not-something-we-protected"));
    }

    [Fact]
    public void Secrets_fails_clearly_when_the_host_has_no_protector_configured()
    {
        var host = new PluginHostCollector("1.0.0", Path.GetTempPath(), "t", new PluginStatusRegistry(), NullLogger.Instance);

        Assert.Throws<InvalidOperationException>(() => host.Secrets.Protect("hunter2"));
    }
}
