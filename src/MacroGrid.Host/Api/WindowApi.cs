using MacroGrid.Host.Ui;
using MacroGrid.Core.Plugins;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints that open the tool windows (each one hosts a page of the editor bundle).</summary>
internal static class WindowApi
{
    public static RouteGroupBuilder MapWindowApi(this RouteGroupBuilder api)
    {
        api.MapPost("/windows/preferences", (IUiWindowService windows) =>
            ShowAsync(windows, "preferences", "Preferences", "preferences", 640, 520));

        api.MapPost("/windows/plugins", (IUiWindowService windows) =>
            ShowAsync(windows, "plugins", "Plugins", "plugins", 640, 520));

        api.MapPost("/windows/plugin-settings/{id}", async (string id, IUiWindowService windows, PluginManager plugins, PluginLocalizer localizer) =>
        {
            var name = localizer.Translate(id, plugins.Plugins.FirstOrDefault(p => p.Id == id)?.Name) ?? id;
            return await ShowAsync(windows, $"plugin-settings-{id}", name, $"plugin-settings&id={Uri.EscapeDataString(id)}", 520, 560);
        });

        api.MapPost("/windows/help", (HttpRequest request, IUiWindowService windows) =>
        {
            var tab = request.Query["tab"].ToString();
            var tabQuery = tab is "about" or "agreement" or "licenses" ? $"&tab={tab}" : "";
            return ShowAsync(windows, "help", "Help", "help" + tabQuery, 760, 560);
        });

        api.MapPost("/windows/pairing", (IUiWindowService windows) =>
            ShowAsync(windows, "pairing", "Pairing", "pairing", 760, 560));

        // ?tab=check (the Help menu's "Check for Updates") makes the window run a check as soon as it opens.
        api.MapPost("/windows/update", (HttpRequest request, IUiWindowService windows) =>
            ShowAsync(windows, "update", "Update", request.Query["tab"] == "check" ? "update&check=1" : "update", 640, 560));

        return api;
    }

    private static async Task<IResult> ShowAsync(IUiWindowService windows, string key, string title, string windowQuery, int width, int height)
    {
        await windows.ShowToolWindowAsync(key, title, $"http://localhost:{ServerApp.Port}/editor/?window={windowQuery}", width, height);
        return Results.NoContent();
    }
}
