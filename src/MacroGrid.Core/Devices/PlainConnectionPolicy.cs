using System.Net;

namespace MacroGrid.Core.Devices;

/// <summary>
/// The "Allow unencrypted connections" preference: when it is off, the plain port (<c>ws://</c>, <c>http://</c>) refuses every connection that does not
/// come from this computer, so a phone has to use the encrypted port. This computer itself always reaches the plain port, because the editor window
/// talks to it there. Connections that are already open are not cut; the rule applies to the next one.
/// </summary>
public static class PlainConnectionPolicy
{
    public static bool IsAllowed(bool allowUnencrypted, int localPort, int plainPort, IPAddress? remote) =>
        allowUnencrypted || localPort != plainPort || LoopbackGuard.IsLoopback(remote);
}
