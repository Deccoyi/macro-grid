using System.Text.Json.Nodes;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Actions;

/// <summary>Sets the master volume to the widget's live dragged value. Bind this to a slider/knob's
/// "valueChange" event — <see cref="ActionContext.Value"/> is what the user just dragged to (0..100),
/// there is no static setting. No-op if fired from any other event (Value is null there).</summary>
public sealed class SetVolumeAction(IAudioService audio) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.setVolume";
    public string Type => TypeId;
    public string DisplayName => "Ana ses seviyesi";
    public string Category => "Ses";
    public string? Description => "Slider/knob ile ana ses seviyesini ayarlar";
    public string? Icon => "volume-2";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        if (context.Value is { } value) audio.SetMasterVolume(value);
        return Task.CompletedTask;
    }
}

/// <summary>Sets (not toggles) mute. Settings: { "muted": bool } — bind the two states of a toggle widget's
/// toggleOn/toggleOff to this with opposite settings, same pattern as any other toggle-driven action.</summary>
public sealed class SetMuteAction(IAudioService audio) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.setMute";
    public string Type => TypeId;
    public string DisplayName => "Sesi kapat/aç";
    public string Category => "Ses";
    public string? Description => "Ana sesi belirli bir duruma getirir";
    public string? Icon => "volume-x";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        audio.SetMuted(settings["muted"]?.GetValue<bool>() ?? true);
        return Task.CompletedTask;
    }

    public static JsonObject Settings(bool muted) => new() { ["muted"] = muted };
}

/// <summary>Flips mute. Settings: none — for a plain button (not a toggle widget) where either state's press should just flip it.</summary>
public sealed class ToggleMuteAction(IAudioService audio) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.toggleMute";
    public string Type => TypeId;
    public string DisplayName => "Sesi sessize al/aç";
    public string Category => "Ses";
    public string? Description => "Ana sesi mute/unmute arasında değiştirir";
    public string? Icon => "volume-1";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        audio.SetMuted(!audio.GetMuted());
        return Task.CompletedTask;
    }
}
