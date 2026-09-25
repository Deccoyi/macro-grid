using MacroGrid.Host.Ui;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Variables;
using MacroGrid.Plugin.Abstractions;
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

        // A SettingFieldKind.File field: title/filter come from the request body (the field's own label
        // and FileFilter), so the dialog matches whatever the action or plugin settings form asked for.
        api.MapPost("/browse/file", async (HttpRequest request, IUiDialogService dialogs) =>
        {
            var (valid, body) = await ApiResults.ReadJsonAsync<BrowseFileRequest>(request);
            if (!valid || body is null) return ApiResults.InvalidJson();

            var path = await dialogs.BrowseForFileAsync(body.Title ?? "", body.Filter ?? "All files (*.*)|*.*");
            return Results.Json(new { path });
        });

        return api;
    }

    private sealed record BrowseFileRequest(string? Title, string? Filter);
}
