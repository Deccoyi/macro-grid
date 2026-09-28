using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints behind the Plugins tool window (docs/design/plugins-tool-window.md): a plugin's own tree
/// items (the optional <see cref="IPluginTreeProvider"/>), one level per request, and an item's settings form (the
/// optional <see cref="IPluginTreeItemSettings"/>). Every route answers 404 for a plugin that does not implement the
/// interface, so nothing here reaches a plugin that did not opt in. Nothing is called until the editor asks, and the
/// editor asks only for nodes the user expands in a tool window that is on screen.</summary>
internal static class PluginTreeApi
{
    public static RouteGroupBuilder MapPluginTreeApi(this RouteGroupBuilder api)
    {
        // What changed since the editor last looked (TreeItemsChanged, and plugins with tree items loading or
        // unloading). Polled only while the Plugins tool window is on screen; since=-1 just returns the revision.
        api.MapGet("/plugins/tree-changes", (long? since, PluginManager plugins) =>
            ApiResults.Json(plugins.TreeChanges.Since(since ?? -1)));

        // One level: parent absent = the plugin's top level; token = the continuation token of the previous page.
        api.MapGet("/plugins/{id}/tree-items", async (string id, string? parent, string? token, HttpContext http, PluginManager plugins, PluginLocalizer localizer) =>
        {
            if (plugins.GetTreeProvider(id) is not { } provider) return Results.NotFound();
            try
            {
                var page = await PluginTreeReader.ReadAsync(provider, parent, token, PluginTreeReader.DefaultTimeout, http.RequestAborted);
                return ApiResults.Json(new
                {
                    Items = page.Items.Select(i => i with
                    {
                        Label = localizer.Translate(id, i.Label)!,
                        Tooltip = localizer.Translate(id, i.Tooltip),
                    }),
                    page.ContinuationToken,
                });
            }
            catch (TimeoutException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status504GatewayTimeout);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new { error = localizer.Translate(id, ex.Message) }, statusCode: StatusCodes.Status502BadGateway);
            }
        });

        api.MapGet("/plugins/{id}/tree-items/settings/schema", (string id, string? item, PluginManager plugins, PluginLocalizer localizer) =>
            RunItemSettings(plugins, id, item, localizer, (settings, itemId) =>
                ApiResults.Json(settings.GetItemFields(itemId).Select(f => localizer.Localize(id, f)).ToList())));

        api.MapGet("/plugins/{id}/tree-items/settings", (string id, string? item, PluginManager plugins, PluginLocalizer localizer) =>
            RunItemSettings(plugins, id, item, localizer, (settings, itemId) =>
                ApiResults.Json(PluginApi.RedactPasswords(settings.GetItemFields(itemId), settings.LoadItem(itemId)))));

        api.MapPut("/plugins/{id}/tree-items/settings", async (string id, string? item, HttpRequest request, PluginManager plugins, PluginLocalizer localizer) =>
        {
            var (valid, values) = await ApiResults.ReadJsonAsync<JsonObject>(request);
            if (!valid || values is null) return ApiResults.InvalidJson();
            return RunItemSettings(plugins, id, item, localizer, (settings, itemId) =>
            {
                PluginApi.RestoreUnchangedPasswords(settings.GetItemFields(itemId), values, () => settings.LoadItem(itemId));
                settings.SaveItem(itemId, values);
                return Results.NoContent();
            });
        });

        // Dynamic dropdowns of an item's settings form, served by the tree provider if it also implements IOptionsSource.
        api.MapPost("/plugins/{id}/tree-items/options/{sourceId}", (string id, string sourceId, HttpRequest request, PluginManager plugins, PluginLocalizer localizer) =>
            OptionsEndpoint.HandleAsync(plugins.GetTreeProvider(id) as IOptionsSource, "This plugin does not provide dynamic options",
                sourceId, request, localizer, () => id));

        return api;
    }

    /// <summary>404 unless the plugin's tree provider implements <see cref="IPluginTreeItemSettings"/>; 400 without an
    /// item id; a plugin that throws (an unknown or removed item) gets a 400 with its message, never a server error.</summary>
    private static IResult RunItemSettings(
        PluginManager plugins, string pluginId, string? itemId, PluginLocalizer localizer, Func<IPluginTreeItemSettings, string, IResult> run)
    {
        if (plugins.GetTreeProvider(pluginId) is not IPluginTreeItemSettings settings) return Results.NotFound();
        if (string.IsNullOrEmpty(itemId)) return ApiResults.BadRequest("Missing item id.");
        try
        {
            return run(settings, itemId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ApiResults.BadRequest(localizer.Translate(pluginId, ex.Message));
        }
    }
}
