namespace MacroGrid.Plugin.Abstractions;

/// <summary>Reads/sets the system's master output volume (the one Windows' own volume slider controls).
/// Per-application volume mixing is out of scope.</summary>
public interface IAudioService
{
    /// <summary>0..100.</summary>
    double GetMasterVolume();

    /// <summary>Clamped to 0..100.</summary>
    void SetMasterVolume(double percent);

    bool GetMuted();

    void SetMuted(bool muted);
}
