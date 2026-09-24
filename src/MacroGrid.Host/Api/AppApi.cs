using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Devices;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Preferences;
using MacroGrid.Core.Sessions;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using MacroGrid.Windows.Autostart;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for app-level state: preferences, legal texts, autostart, version, status and the visible-window list.</summary>
internal static class AppApi
{
    public static RouteGroupBuilder MapAppApi(this RouteGroupBuilder api)
    {
        api.MapGet("/preferences", (PreferencesStore preferences) =>
            ApiResults.Json(preferences.Get()));

        api.MapPut("/preferences", async (HttpRequest request, PreferencesStore preferences) =>
        {
            var (valid, parsed) = await ApiResults.ReadJsonAsync<AppPreferences>(request);
            if (!valid || parsed is null)
                return ApiResults.InvalidJson();

            preferences.Save(parsed);
            return Results.NoContent();
        });

        api.MapGet("/legal", (LegalDocuments legal) => ApiResults.Json(legal.GetOverview()));

        api.MapGet("/legal/library/{name}", (string name, LegalDocuments legal) =>
            legal.GetLibrary(name) is { } text ? Results.Text(text, "text/plain; charset=utf-8") : Results.NotFound());

        api.MapGet("/system/autostart", (AutostartService autostart) => Results.Json(new { enabled = autostart.IsEnabled() }));

        api.MapPut("/system/autostart", async (HttpRequest request, AutostartService autostart) =>
        {
            JsonNode? body;
            try
            {
                body = await JsonNode.ParseAsync(request.Body);
            }
            catch (JsonException)
            {
                return ApiResults.InvalidJson();
            }
            if (body?["enabled"] is not JsonValue value || !value.TryGetValue<bool>(out var enabled))
                return ApiResults.BadRequest("\"enabled\" (true/false) is required.");

            autostart.SetEnabled(enabled);
            return Results.Json(new { enabled = autostart.IsEnabled() });
        });

        api.MapGet("/system/windows", (IActiveWindowSource windowSource) => windowSource.ListVisibleWindows());

        api.MapGet("/status", (PluginStatusRegistry statusRegistry, PluginLocalizer localizer, PreferencesStore preferences) =>
            statusRegistry.All.Select(s => s.PluginId == "core"
                ? s with { Text = LocalizeCoreStatus(s, preferences.Get().Language, localizer) }
                : s with { Text = localizer.Translate(s.PluginId, s.Text)!, Tooltip = localizer.Translate(s.PluginId, s.Tooltip) }));

        api.MapGet("/version", () => new { version = ClientHub.ServerVersion });

        return api;
    }

    /// <summary>The core status items are written in English; the device counter is the one that carries words to translate.</summary>
    private static string LocalizeCoreStatus(PluginStatusEntry status, string language, PluginLocalizer localizer)
    {
        if (status.Id == "actionError") return localizer.TranslateAny(status.Text) ?? status.Text;
        if (status.Id != "devices" || !int.TryParse(status.Text.Split(' ')[0], out var count)) return status.Text;
        return language == "tr" ? $"{count} cihaz" : count == 1 ? "1 device" : $"{count} devices";
    }
}
