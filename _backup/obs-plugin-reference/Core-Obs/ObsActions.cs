using System.Text.Json.Nodes;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Obs;

/// <summary>Switches the OBS program scene. Settings: { "sceneName": string }</summary>
public sealed class ObsSetSceneAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.setScene";
    public string Type => TypeId;
    public string DisplayName => "OBS: Sahne değiştir";

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var sceneName = settings["sceneName"]?.GetValue<string>();
        return string.IsNullOrEmpty(sceneName)
            ? Task.CompletedTask
            : obs.RequestAsync("SetCurrentProgramScene", new JsonObject { ["sceneName"] = sceneName }, cancellationToken);
    }

    public static JsonObject Settings(string sceneName) => new() { ["sceneName"] = sceneName };
}

/// <summary>Starts the OBS stream. No settings.</summary>
public sealed class ObsStartStreamAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.startStream";
    public string Type => TypeId;
    public string DisplayName => "OBS: Yayını başlat";
    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        obs.RequestAsync("StartStream", null, cancellationToken);
}

/// <summary>Stops the OBS stream. No settings.</summary>
public sealed class ObsStopStreamAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.stopStream";
    public string Type => TypeId;
    public string DisplayName => "OBS: Yayını durdur";
    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        obs.RequestAsync("StopStream", null, cancellationToken);
}

/// <summary>Toggles the OBS stream on/off. No settings.</summary>
public sealed class ObsToggleStreamAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.toggleStream";
    public string Type => TypeId;
    public string DisplayName => "OBS: Yayını aç/kapat";
    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        obs.RequestAsync("ToggleStream", null, cancellationToken);
}

/// <summary>Starts OBS recording. No settings.</summary>
public sealed class ObsStartRecordAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.startRecord";
    public string Type => TypeId;
    public string DisplayName => "OBS: Kaydı başlat";
    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        obs.RequestAsync("StartRecord", null, cancellationToken);
}

/// <summary>Stops OBS recording. No settings.</summary>
public sealed class ObsStopRecordAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.stopRecord";
    public string Type => TypeId;
    public string DisplayName => "OBS: Kaydı durdur";
    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        obs.RequestAsync("StopRecord", null, cancellationToken);
}

/// <summary>Toggles OBS recording on/off. No settings.</summary>
public sealed class ObsToggleRecordAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.toggleRecord";
    public string Type => TypeId;
    public string DisplayName => "OBS: Kaydı aç/kapat";
    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken) =>
        obs.RequestAsync("ToggleRecord", null, cancellationToken);
}

/// <summary>Mutes/unmutes an OBS audio input. Settings: { "inputName": string, "muted": bool }</summary>
public sealed class ObsSetMuteAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.setMute";
    public string Type => TypeId;
    public string DisplayName => "OBS: Sesi kapat/aç";

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var inputName = settings["inputName"]?.GetValue<string>();
        if (string.IsNullOrEmpty(inputName)) return Task.CompletedTask;
        var muted = settings["muted"]?.GetValue<bool>() ?? true;
        return obs.RequestAsync("SetInputMute", new JsonObject { ["inputName"] = inputName, ["inputMuted"] = muted }, cancellationToken);
    }

    public static JsonObject Settings(string inputName, bool muted) => new() { ["inputName"] = inputName, ["muted"] = muted };
}

/// <summary>Toggles mute on an OBS audio input. Settings: { "inputName": string }</summary>
public sealed class ObsToggleMuteAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.toggleMute";
    public string Type => TypeId;
    public string DisplayName => "OBS: Ses sessize al/aç";

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var inputName = settings["inputName"]?.GetValue<string>();
        return string.IsNullOrEmpty(inputName)
            ? Task.CompletedTask
            : obs.RequestAsync("ToggleInputMute", new JsonObject { ["inputName"] = inputName }, cancellationToken);
    }

    public static JsonObject Settings(string inputName) => new() { ["inputName"] = inputName };
}

/// <summary>Sets an OBS audio input's volume. Settings: { "inputName": string, "volume": number } — volume is a
/// 0..1 linear multiplier (matches obs-websocket's inputVolumeMul), not dB; the slider widget maps its 0..100 to this.</summary>
public sealed class ObsSetVolumeAction(ObsConnection obs) : IActionHandler
{
    public const string TypeId = "obs.setVolume";
    public string Type => TypeId;
    public string DisplayName => "OBS: Ses seviyesi";

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var inputName = settings["inputName"]?.GetValue<string>();
        if (string.IsNullOrEmpty(inputName)) return Task.CompletedTask;
        var volume = settings["volume"]?.GetValue<double>() ?? 1.0;
        return obs.RequestAsync("SetInputVolume", new JsonObject { ["inputName"] = inputName, ["inputVolumeMul"] = Math.Clamp(volume, 0, 1) }, cancellationToken);
    }

    public static JsonObject Settings(string inputName, double volume) => new() { ["inputName"] = inputName, ["volume"] = volume };
}
