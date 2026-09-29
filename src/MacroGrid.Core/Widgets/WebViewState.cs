using System.Collections.Concurrent;

namespace MacroGrid.Core.Widgets;

/// <summary>What each device currently shows in its <c>web</c> widgets when a <c>core.web</c> button changed it: a live view state per device,
/// kept in memory only (never written to the profile, gone when the server restarts). Cleared when the device switches profile or the profile is saved.</summary>
public sealed class WebViewState
{
    private sealed class DeviceState
    {
        public readonly ConcurrentDictionary<string, string> Urls = new();
        public readonly ConcurrentDictionary<string, int> Reloads = new();
    }

    private readonly ConcurrentDictionary<string, DeviceState> _devices = new();

    public string? GetUrl(string deviceId, string widgetId) =>
        _devices.TryGetValue(deviceId, out var d) && d.Urls.TryGetValue(widgetId, out var url) ? url : null;

    public int GetReload(string deviceId, string widgetId) =>
        _devices.TryGetValue(deviceId, out var d) && d.Reloads.TryGetValue(widgetId, out var n) ? n : 0;

    public void SetUrl(string deviceId, string widgetId, string url) => For(deviceId).Urls[widgetId] = url;

    public void ResetUrl(string deviceId, string widgetId)
    {
        if (_devices.TryGetValue(deviceId, out var d))
            d.Urls.TryRemove(widgetId, out _);
    }

    /// <returns>The new reload counter of that widget on that device.</returns>
    public int Reload(string deviceId, string widgetId) => For(deviceId).Reloads.AddOrUpdate(widgetId, 1, (_, n) => n + 1);

    /// <returns>The ids of the widgets that had a different address, so the caller can tell the device to go back to the profile's.</returns>
    public IReadOnlyList<string> Clear(string deviceId) =>
        _devices.TryRemove(deviceId, out var d) ? [.. d.Urls.Keys] : [];

    private DeviceState For(string deviceId) => _devices.GetOrAdd(deviceId, _ => new DeviceState());
}
