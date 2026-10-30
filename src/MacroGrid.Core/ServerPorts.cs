namespace MacroGrid.Core;

/// <summary>The port a packaged server listens on. Kept in one place so what the server binds and what a plugin may never reach (and what the
/// plugin tool refuses while it tries a plugin out) cannot drift apart.</summary>
public static class ServerPorts
{
    /// <summary>The plain HTTP and WebSocket port; the TLS one is the next number.</summary>
    public const int Default = 9820;
}
