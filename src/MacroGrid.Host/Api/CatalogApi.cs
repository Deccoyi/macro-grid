using System.Text.Json.Nodes;
using MacroGrid.Core;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Windows.Autostart;
using MacroGrid.Windows.Windows;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints that describe what can be used in a profile: the action catalog, dynamic options, variables and file pickers.</summary>
internal static class CatalogApi
{
    public static RouteGroupBuilder MapCatalogApi(this RouteGroupBuilder api)
    {
        api.MapGet("/actions", (ActionDispatcher dispatcher, PluginManager plugins, PluginLocalizer localizer) =>
            dispatcher.Handlers.Select(h =>
            {
                var descriptor = h as IActionDescriptor;
                var pluginId = plugins.GetActionPluginId(h.Type);
                return new
                {
                    h.Type,
                    DisplayName = localizer.Translate(pluginId, h.DisplayName)!,
                    Category = localizer.Translate(pluginId, descriptor?.Category ?? "Other")!,
                    Description = localizer.Translate(pluginId, descriptor?.Description),
                    Icon = descriptor?.Icon,
                    PluginId = pluginId,
                    Fields = descriptor is { Fields.Count: > 0 } ? descriptor.Fields.Select(f => localizer.Localize(pluginId, f)).ToList() : null,
                };
            }).OrderBy(a => a.DisplayName));

        api.MapPost("/actions/{type}/options/{sourceId}", async (string type, string sourceId, HttpRequest request, ActionDispatcher dispatcher, PluginManager plugins, PluginLocalizer localizer) =>
        {
            var handler = dispatcher.Handlers.FirstOrDefault(h => h.Type == type);
            return await OptionsEndpoint.HandleAsync(handler as IOptionsSource, "This action does not provide dynamic options",
                sourceId, request, localizer, () => plugins.GetActionPluginId(type));
        });

        api.MapGet("/variables/snapshot", (VariableStore variables) =>
            ApiResults.Json(variables.Snapshot()));

        api.MapGet("/variables/catalog", (VariableCatalog catalog, PluginLocalizer localizer) => catalog.Localized(localizer));

        api.MapPost("/browse/executable", async (IUiDialogService dialogs) =>
        {
            var path = await dialogs.BrowseForExecutableAsync();
            return Results.Json(new { path });
        });

        return api;
    }
}
