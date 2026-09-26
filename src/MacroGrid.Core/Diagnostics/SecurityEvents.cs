using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Diagnostics;

/// <summary>
/// Event ids of security-relevant log lines: pairing, removed devices and plugin installs and permissions.
/// Their messages start with "Security:" so they can be found in the log files. They never contain a PIN or
/// a token. The log files are kept for a limited time only (<see cref="LogRetention"/>).
/// </summary>
public static class SecurityEvents
{
    public static readonly EventId PairingOpened = new(1001, nameof(PairingOpened));
    public static readonly EventId DevicePaired = new(1002, nameof(DevicePaired));
    public static readonly EventId WrongPin = new(1003, nameof(WrongPin));
    public static readonly EventId PinWhileClosed = new(1004, nameof(PinWhileClosed));
    public static readonly EventId PairingBlocked = new(1005, nameof(PairingBlocked));
    public static readonly EventId PinRenewed = new(1006, nameof(PinRenewed));
    public static readonly EventId DeviceRemoved = new(1007, nameof(DeviceRemoved));
    public static readonly EventId DeviceTokenUnreadable = new(1008, nameof(DeviceTokenUnreadable));
    public static readonly EventId PluginInstalled = new(1101, nameof(PluginInstalled));
    public static readonly EventId PluginPermissionsGranted = new(1102, nameof(PluginPermissionsGranted));
    public static readonly EventId PluginUninstalled = new(1103, nameof(PluginUninstalled));
}
