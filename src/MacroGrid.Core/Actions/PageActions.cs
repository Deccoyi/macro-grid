using System.Text.Json.Nodes;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Core.Actions;

/// <summary>Changes the page shown on the triggering device. Settings: { "mode": "goto"|"next"|"prev"|"back", "pageId"?: string, "profileId"?: string }. With a profileId the device switches to that profile first (a rule has no open profile); a step saved without one only shows the page.</summary>
public sealed class PageAction : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.page";

    public string Type => TypeId;
    public string DisplayName => "Switch page";
    public string Category => "Page & Profile";
    public string? Description => "Changes the page the device shows";
    public string? Icon => "layout-grid";
    public IReadOnlyList<SettingField> Fields => [];

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

    private static async Task GotoAsync(ActionContext context, JsonObject settings)
    {
        var pageId = settings["pageId"]?.GetValue<string>();
        if (string.IsNullOrEmpty(pageId)) return;
        if (settings["profileId"]?.GetValue<string>() is { Length: > 0 } profileId) await context.Device.SwitchProfileAsync(profileId);
        await context.Device.ShowPageAsync(pageId);
    }

    public static JsonObject Goto(string pageId) => new() { ["mode"] = "goto", ["pageId"] = pageId };
    public static JsonObject Next() => new() { ["mode"] = "next" };
    public static JsonObject Previous() => new() { ["mode"] = "prev" };
    public static JsonObject Back() => new() { ["mode"] = "back" };
}

/// <summary>Switches the triggering device to a different profile. Settings: { "profileId": string }</summary>
public sealed class ProfileAction : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.profile";

    public string Type => TypeId;
    public string DisplayName => "Switch profile";
    public string Category => "Page & Profile";
    public string? Description => "Changes the profile of the device";
    public string? Icon => "user-cog";
    public IReadOnlyList<SettingField> Fields => [];

    public Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var profileId = settings["profileId"]?.GetValue<string>();
        return string.IsNullOrEmpty(profileId) ? Task.CompletedTask : context.Device.SwitchProfileAsync(profileId);
    }

    public static JsonObject Settings(string profileId) => new() { ["profileId"] = profileId };
}
