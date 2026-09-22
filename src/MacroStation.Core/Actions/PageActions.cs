using System.Text.Json.Nodes;
using MacroStation.Plugin.Abstractions;

namespace MacroStation.Core.Actions;

/// <summary>Changes the page shown on the triggering device. Settings: { "mode": "goto"|"next"|"prev"|"back", "pageId"?: string }</summary>
public sealed class PageAction : IActionHandler
{
    public const string TypeId = "core.page";

    public string Type => TypeId;
    public string DisplayName => "Sayfa değiştir";

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var mode = settings["mode"]?.GetValue<string>() ?? "goto";
        return mode switch
        {
            "next" => context.Device.NextPageAsync(),
            "prev" => context.Device.PreviousPageAsync(),
            "back" => context.Device.BackAsync(),
            _ => GotoAsync(context, settings),
        };
    }

    private static Task GotoAsync(ActionContext context, JsonObject settings)
    {
        var pageId = settings["pageId"]?.GetValue<string>();
        return string.IsNullOrEmpty(pageId) ? Task.CompletedTask : context.Device.ShowPageAsync(pageId);
    }

    public static JsonObject Goto(string pageId) => new() { ["mode"] = "goto", ["pageId"] = pageId };
    public static JsonObject Next() => new() { ["mode"] = "next" };
    public static JsonObject Previous() => new() { ["mode"] = "prev" };
    public static JsonObject Back() => new() { ["mode"] = "back" };
}

/// <summary>Switches the triggering device to a different profile. Settings: { "profileId": string }</summary>
public sealed class ProfileAction : IActionHandler
{
    public const string TypeId = "core.profile";

    public string Type => TypeId;
    public string DisplayName => "Profil değiştir";

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var profileId = settings["profileId"]?.GetValue<string>();
        return string.IsNullOrEmpty(profileId) ? Task.CompletedTask : context.Device.SwitchProfileAsync(profileId);
    }

    public static JsonObject Settings(string profileId) => new() { ["profileId"] = profileId };
}
