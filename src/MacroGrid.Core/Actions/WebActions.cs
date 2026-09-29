using System.Text.Json.Nodes;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Model;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Web;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using Microsoft.Extensions.Logging;

namespace MacroGrid.Core.Actions;

/// <summary>Changes what a named <c>web</c> widget shows on the device that pressed the button. Settings: { "widgetId": string,
/// "mode": "set"|"reset"|"reload", "url"?: string }. Only that device is affected; the change is not written to the profile.
/// The address follows the same rule as the widget's own (<see cref="WebUrlRule"/>). It is a core action on purpose: a plugin gets no way
/// to point a widget at any address.</summary>
public sealed class WebAction(SessionRegistry sessions, ProfileStore profiles, WebViewState state, ILogger<WebAction> logger) : IActionHandler, IActionDescriptor
{
    public const string TypeId = "core.web";

    public string Type => TypeId;
    public string DisplayName => "Change web page";
    public string Category => "Widgets";
    public string? Description => "Shows another address in a web widget, or reloads it, on the device that pressed the button";
    public string? Icon => "globe";
    public IReadOnlyList<SettingField> Fields => [];

    public async Task ExecuteAsync(ActionContext context, JsonObject settings, CancellationToken cancellationToken)
    {
        var widgetId = settings["widgetId"]?.GetValue<string>();
        var mode = settings["mode"]?.GetValue<string>() ?? "set";
        if (string.IsNullOrEmpty(widgetId))
            throw new InvalidOperationException("Choose the web widget this button changes.");

        var targets = sessions.All.Where(s => s.DeviceId == context.DeviceId).ToList();
        var profileId = targets.Select(s => s.ProfileId).FirstOrDefault(id => id is not null);
        var widget = profileId is null ? null : profiles.Get(profileId)?.Pages.SelectMany(p => p.Widgets).FirstOrDefault(w => w.Id == widgetId);
        if (widget is not { Type: WidgetTypes.Web })
            throw new InvalidOperationException("The web widget this button changes is missing.");

        WidgetStateMessage message;
        switch (mode)
        {
            case "set":
                var url = settings["url"]?.GetValue<string>();
                if (!WebUrlRule.IsAllowed(url))
                {
                    var host = WebUrlRule.HostForLog(url);
                    logger.LogWarning(SecurityEvents.WebUrlRefused, "Security: a web address from a button was refused (host: {Host})", SecurityEvents.ForLog(host));
                    throw new InvalidOperationException("That web address is not allowed: only http and https pages, without a user name or password, and not this computer.");
                }
                state.SetUrl(context.DeviceId, widgetId, url!);
                message = new WidgetStateMessage(widgetId, Url: url);
                break;
            case "reset":
                state.ResetUrl(context.DeviceId, widgetId);
                message = new WidgetStateMessage(widgetId, Url: "");
                break;
            case "reload":
                message = new WidgetStateMessage(widgetId, Reload: state.Reload(context.DeviceId, widgetId));
                break;
            default:
                throw new InvalidOperationException($"Unknown web action mode '{SecurityEvents.ForLog(mode)}'.");
        }

        foreach (var session in targets)
            await session.SendAsync(MessageTypes.WidgetState, message, cancellationToken);
    }

    public static JsonObject Set(string widgetId, string url) => new() { ["widgetId"] = widgetId, ["mode"] = "set", ["url"] = url };
    public static JsonObject Reset(string widgetId) => new() { ["widgetId"] = widgetId, ["mode"] = "reset" };
    public static JsonObject Reload(string widgetId) => new() { ["widgetId"] = widgetId, ["mode"] = "reload" };
}
