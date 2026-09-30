using System.Runtime.InteropServices;
using MacroGrid.Plugin.Abstractions;
using NAudio.CoreAudioApi;

namespace MacroGrid.Windows.Audio;

/// <summary>
/// Master volume via WASAPI's default render endpoint (<c>IAudioEndpointVolume</c>, wrapped by
/// NAudio.Wasapi's <see cref="AudioEndpointVolume"/>) — the same one Windows' own volume slider/OSD
/// controls. One enumerator and the default device are kept, because the volume variables are read every
/// second. The default output device can change at any time (headphones plugged in, HDMI switched), so the
/// cached device is dropped when Windows reports a change, and also re-resolved every
/// <see cref="MaxDeviceAge"/> as a safety net. The notification callback only sets a flag: it arrives on a
/// COM thread and must not touch the device itself.
/// </summary>
public sealed class WindowsAudioService : IAudioService, IDisposable
{
    private static readonly TimeSpan MaxDeviceAge = TimeSpan.FromSeconds(30);

    private readonly Lock _lock = new();
    private readonly DeviceChangeWatcher _watcher;
    private MMDeviceEnumerator? _enumerator;
    private MMDevice? _device;
    private long _resolvedAt;
    private volatile bool _stale = true;
    private bool _disposed;

    public WindowsAudioService()
    {
        _watcher = new DeviceChangeWatcher(() => _stale = true);
    }

    public double GetMasterVolume() => WithDevice(d => d.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0, 0.0);

    public void SetMasterVolume(double percent) =>
        WithDevice(d =>
        {
            d.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(Math.Clamp(percent, 0, 100) / 100.0);
            return true;
        }, false);

    public bool GetMuted() => WithDevice(d => d.AudioEndpointVolume.Mute, false);

    public void SetMuted(bool muted) =>
        WithDevice(d =>
        {
            d.AudioEndpointVolume.Mute = muted;
            return true;
        }, false);

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _watcher.Dispose();
            DropDevice();
            _enumerator?.Dispose();
            _enumerator = null;
        }
    }

    /// <summary>Runs <paramref name="read"/> on the default device. A device that vanished between the lookup and
    /// the call (unplugged) is dropped and the call is tried once more on the new default.</summary>
    private T WithDevice<T>(Func<MMDevice, T> read, T fallback)
    {
        lock (_lock)
        {
            if (_disposed) return fallback;
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var device = GetDevice();
                if (device is null) return fallback;
                try
                {
                    return read(device);
                }
                catch (COMException)
                {
                    DropDevice();
                }
            }
            return fallback;
        }
    }

    private MMDevice? GetDevice()
    {
        if (_device is not null && !_stale && Environment.TickCount64 - _resolvedAt < MaxDeviceAge.TotalMilliseconds)
            return _device;

        DropDevice();
        _stale = false;
        try
        {
            _enumerator ??= new MMDeviceEnumerator();
            _device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _resolvedAt = Environment.TickCount64;
        }
        catch (COMException)
        {
            // No active playback device (none plugged in, or Windows Audio service stopped) — treat as
            // "no volume to report", same as any other external dependency being unavailable. The next call
            // looks again.
            _device = null;
        }
        return _device;
    }

    private void DropDevice()
    {
        _device?.Dispose();
        _device = null;
    }

    /// <summary>Subscribes to endpoint changes on its own enumerator and ends the subscription on dispose, so the
    /// callback never outlives the service. The events arrive on a Windows audio worker thread, so the handler
    /// only raises the flag.</summary>
    private sealed class DeviceChangeWatcher : IDisposable
    {
        private MMDeviceEnumerator? _enumerator;
        private MMDeviceNotificationClient? _client;

        public DeviceChangeWatcher(Action changed)
        {
            try
            {
                _enumerator = new MMDeviceEnumerator();
                _client = _enumerator.CreateNotificationClient(useSynchronizationContext: false);
                _client.DefaultDeviceChanged += (_, _) => changed();
                _client.DeviceStateChanged += (_, _) => changed();
                _client.DeviceAdded += (_, _) => changed();
                _client.DeviceRemoved += (_, _) => changed();
            }
            catch (COMException)
            {
                // Without the callback only the periodic re-resolve keeps the device current.
                Dispose();
            }
        }

        public void Dispose()
        {
            _client?.Dispose();
            _client = null;
            _enumerator?.Dispose();
            _enumerator = null;
        }
    }
}
