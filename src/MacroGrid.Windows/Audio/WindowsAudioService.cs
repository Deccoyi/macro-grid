using System.Runtime.InteropServices;
using MacroGrid.Plugin.Abstractions;
using NAudio.CoreAudioApi;

namespace MacroGrid.Windows.Audio;

/// <summary>
/// Master volume via WASAPI's default render endpoint (<c>IAudioEndpointVolume</c>, wrapped by
/// NAudio.Wasapi's <see cref="AudioEndpointVolume"/>) — the same one Windows' own volume slider/OSD
/// controls. A new <see cref="MMDeviceEnumerator"/>/device handle is resolved on every call rather than
/// cached: the default output device can change at any time (headphones plugged in, HDMI switched), and
/// re-resolving is cheap compared to a slider drag's ~10Hz rate.
/// </summary>
public sealed class WindowsAudioService : IAudioService
{
    public double GetMasterVolume()
    {
        using var device = GetDefaultDevice();
        return device is null ? 0 : device.AudioEndpointVolume.MasterVolumeLevelScalar * 100.0;
    }

    public void SetMasterVolume(double percent)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;
        device.AudioEndpointVolume.MasterVolumeLevelScalar = (float)(Math.Clamp(percent, 0, 100) / 100.0);
    }

    public bool GetMuted()
    {
        using var device = GetDefaultDevice();
        return device?.AudioEndpointVolume.Mute ?? false;
    }

    public void SetMuted(bool muted)
    {
        using var device = GetDefaultDevice();
        if (device is null) return;
        device.AudioEndpointVolume.Mute = muted;
    }

    private static MMDevice? GetDefaultDevice()
    {
        using var enumerator = new MMDeviceEnumerator();
        try
        {
            return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch (COMException)
        {
            // No active playback device (none plugged in, or Windows Audio service stopped) — treat as
            // "no volume to report", same as any other external dependency being unavailable.
            return null;
        }
    }
}
