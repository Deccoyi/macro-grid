using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>Sets the master volume to the widget's live dragged value. Bind this to a slider/knob's
/// "valueChange" event — <see cref="ActionContext.Value"/> is what the user just dragged to (0..100),
/// there is no static setting. No-op if fired from any other event (Value is null there).</summary>
public sealed class SetVolumeAction(IAudioService audio) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.setVolume";
    public string Type => TypeId;
    public string DisplayName => "Master volume";
    public string Category => "Audio";
    public string? Description => "Sets the master volume with a slider/knob";
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
    public string DisplayName => "Set mute";
    public string Category => "Audio";
    public string? Description => "Sets the master sound to a specific state";
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
    public string DisplayName => "Toggle mute";
    public string Category => "Audio";
    public string? Description => "Switches the master sound between muted and unmuted";
    public string? Icon => "volume-1";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        audio.SetMuted(!audio.GetMuted());
        return Task.CompletedTask;
    }
}
