using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Windows.Audio;

/// <summary>
/// Publishes "system.audio.*" from <see cref="IAudioService"/> once a second — same polling cadence and
/// pattern as <see cref="Variables.SystemMetricsProvider"/> (see its doc comment). Polling instead of a
/// WASAPI change-notification callback is a deliberate simplicity/risk trade-off: the callback interface
/// (<c>IAudioEndpointVolumeCallback</c>) must marshal back onto the right apartment/thread and unregister
/// itself correctly or it leaks — a once-a-second poll can't get that wrong. <c>system.audio.master</c> is
/// what a slider/knob widget binds its <c>props.valueVariable</c> to (see WidgetStateService), so the UI
/// reflects the volume even when it was changed by something other than this app (the hardware keys,
/// another app, Windows' own flyout).
/// </summary>
public sealed class SystemAudioProvider(IAudioService audio) : IVariableProvider, IVariableCatalogSource
{
    private const string Category = "Ses";

    public IEnumerable<VariableInfo> Describe() =>
    [
        new("system.audio.master", "Ana ses seviyesi (%)", "{system.audio.master|0}%", Category),
        new("system.audio.muted", "Ses sessize alınmış mı", "{system.audio.muted}", Category),
    ];

    public async Task RunAsync(IVariableStore store, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        do
        {
            store.Set("system.audio.master", audio.GetMasterVolume());
            store.Set("system.audio.muted", audio.GetMuted());
        } while (await timer.WaitForNextTickAsync(cancellationToken));
    }
}
