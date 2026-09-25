using MacroGrid.Host.Ui;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins;
using MacroGrid.Plugin.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for installed plugins: listing, install/approve/reload/uninstall, icon packs and settings.</summary>
internal static class PluginApi
{
    public static RouteGroupBuilder MapPluginApi(this RouteGroupBuilder api)
    {
        api.MapGet("/plugins", (PluginManager plugins, PluginLocalizer localizer) =>
            plugins.Plugins.Select(p => p with { Name = localizer.Translate(p.Id, p.Name)! }));

        api.MapGet("/icon-packs", (PluginManager plugins, PluginLocalizer localizer) =>
            plugins.IconPacksWithOwner.Select(o => new { o.Pack.Id, DisplayName = localizer.Translate(o.PluginId, o.Pack.DisplayName)!, Icons = o.Pack.IconNames }));

        api.MapGet("/icon-packs/{packId}/{iconName}", (string packId, string iconName, PluginManager plugins) =>
        {
            var pack = plugins.IconPacks.FirstOrDefault(p => p.Id == packId);
            var svg = pack?.GetIconSvg(iconName);
            return svg is null ? Results.NotFound() : Results.Text(svg, "image/svg+xml");
        });

        // The manifest's optional logo (LoadedPlugin.HasIcon) — the editor's Plugins window shows this
        // instead of the generic category glyph when present.
        api.MapGet("/plugins/{id}/icon", (string id, PluginManager plugins) =>
        {
            var path = plugins.GetPluginIconPath(id);
            if (path is null) return Results.NotFound();
            var contentType = Path.GetExtension(path).Equals(".svg", StringComparison.OrdinalIgnoreCase) ? "image/svg+xml" : "image/png";
            return Results.File(path, contentType);
        });

        api.MapPost("/plugins/install", async (IUiDialogService dialogs, PluginManager plugins) =>
        {
            var sourceDir = await dialogs.BrowseForFolderAsync("Choose the plugin folder (must contain plugin.json)");
            if (sourceDir is null) return Results.Json(new { installed = false, canceled = true });

            if (!File.Exists(Path.Combine(sourceDir, "plugin.json")))
                return ApiResults.BadRequest($"No plugin.json in the selected folder: {sourceDir}");

            try
            {
                var result = await plugins.InstallFromFolderAsync(sourceDir);
                return ApiResults.Json(new { installed = true, id = result.Id, name = result.Name, status = result.Plugin.Status, detail = result.Plugin.Detail });
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return ApiResults.BadRequest(ex.Message);
            }
        });

        // The user approved the permissions a JS plugin declares (shown to them in the Plugins window first).
        api.MapPost("/plugins/{id}/approve", async (string id, PluginManager plugins) =>
            await plugins.ApproveAsync(id) is { } info ? ApiResults.Json(info) : Results.NotFound());

        api.MapPost("/plugins/{id}/reload", async (string id, PluginManager plugins) =>
            await plugins.ReloadAsync(id) is { } info ? ApiResults.Json(info) : Results.NotFound());

        // Unloads the plugin (its actions, variables and status items disappear immediately) and deletes its
        // folder. Plugin DLLs are loaded from memory so they are not locked; if a file is still in use anyway
        // the folder is marked and removed at the next start ("pending").
        api.MapDelete("/plugins/{id}", async (string id, PluginManager plugins) =>
            await plugins.UninstallAsync(id) is { } result
                ? Results.Json(new { removed = result.Removed, pending = result.Pending })
                : Results.NotFound());

        return api.MapPluginSettingsApi();
    }

    private static RouteGroupBuilder MapPluginSettingsApi(this RouteGroupBuilder api)
    {
        // A registered IPluginSettingsPage (schema-driven form) takes precedence; a plugin without one
        // falls back to the raw settings.json passthrough it always had (see docs/plugin-authoring.md
        // "Settings") so older plugins keep working unchanged.
        api.MapGet("/plugins/{id}/settings/schema", (string id, PluginManager plugins, PluginLocalizer localizer) =>
            plugins.GetSettingsPage(id) is { } page
                ? ApiResults.Json(page.Fields.Select(f => localizer.Localize(id, f)).ToList())
                : Results.NotFound());

        api.MapGet("/plugins/{id}/settings", (string id, PluginManager plugins) =>
        {
            if (plugins.GetSettingsPage(id) is { } page)
                return ApiResults.Json(page.Load());

            var dir = plugins.GetPluginDir(id);
            if (dir is null) return Results.NotFound();
            var path = Path.Combine(dir, "settings.json");
            return File.Exists(path) ? Results.Text(File.ReadAllText(path), "application/json") : Results.NotFound();
        });

        api.MapPut("/plugins/{id}/settings", async (string id, HttpRequest request, PluginManager plugins) =>
        {
            if (plugins.GetSettingsPage(id) is { } page)
            {
                var (valid, values) = await ApiResults.ReadJsonAsync<JsonObject>(request);
                if (!valid || values is null) return ApiResults.InvalidJson();
                page.Save(values);
                return Results.NoContent();
            }

            var dir = plugins.GetPluginDir(id);
            if (dir is null) return Results.NotFound();

            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            try { JsonDocument.Parse(body); }
            catch (JsonException) { return ApiResults.InvalidJson(); }

            await File.WriteAllTextAsync(Path.Combine(dir, "settings.json"), body);
            return Results.NoContent();
        });

        api.MapPost("/plugins/{id}/settings/options/{sourceId}", (string id, string sourceId, HttpRequest request, PluginManager plugins, PluginLocalizer localizer) =>
            OptionsEndpoint.HandleAsync(plugins.GetSettingsPage(id) as IOptionsSource, "This plugin does not provide dynamic options",
                sourceId, request, localizer, () => id));

        // Runs a SettingFieldKind.Button field's command (whatever the plugin uses it for — a preview, a
        // connection test, ...): 404 when the settings page does not implement ISettingsCommandHandler at
        // all, so an older/simpler plugin never gets a broken button.
        api.MapPost("/plugins/{id}/settings/command", async (string id, HttpRequest request, PluginManager plugins, PluginLocalizer localizer) =>
        {
            if (plugins.GetSettingsPage(id) is not ISettingsCommandHandler handler)
                return Results.NotFound();

            var (valid, body) = await ApiResults.ReadJsonAsync<SettingsCommandRequest>(request);
            if (!valid || body is null || string.IsNullOrEmpty(body.Command)) return ApiResults.InvalidJson();

            try
            {
                var text = await handler.RunCommandAsync(body.Command, body.Values ?? [], request.HttpContext.RequestAborted);
                return ApiResults.Json(new { text = localizer.Translate(id, text) });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ApiResults.Json(new { text = localizer.Translate(id, ex.Message) });
            }
        });

        return api;
    }

    private sealed record SettingsCommandRequest(string? Command, JsonObject? Values);
}
