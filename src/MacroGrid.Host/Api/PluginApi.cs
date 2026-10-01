using MacroGrid.Host.Ui;
using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Distribution;
using MacroGrid.Plugin.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for installed plugins: listing, install/approve/reload/uninstall, icon packs and settings.</summary>
internal static class PluginApi
{
    public static RouteGroupBuilder MapPluginApi(this RouteGroupBuilder api)
    {
        // trust is Official / ThirdParty / Local — Local also covers a plugin installed before this feature
        // existed (from a folder), which has no recorded origin.
        api.MapGet("/plugins", (PluginManager plugins, PluginLocalizer localizer, PluginInstallOriginStore origins, OfficialCatalog official) =>
            plugins.Plugins.Select(p => new
            {
                p.Id,
                Name = localizer.Translate(p.Id, p.Name)!,
                p.Version,
                p.Status,
                p.Detail,
                p.HasSettings,
                p.PendingPermissions,
                p.HasIcon,
                p.HasTreeItems,
                p.Permissions,
                p.SwitchedOffPermissions,
                Trust = (origins.Get(p.Id)?.Trust ?? PluginTrust.Local).ToString(),
                // From the saved catalog copy only: this list is read often and must not use the network.
                Withdrawn = origins.Get(p.Id)?.Trust == PluginTrust.Official
                    && official.Index?.Plugins.FirstOrDefault(e => e.Id == p.Id)?.Versions.Any(v => v.Withdrawn && v.Version == p.Version) == true,
            }));

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

        // Browses for a folder and reads its plugin.json without installing anything yet, so the editor can
        // ask for consent before the second call actually installs it: a native (C#) plugin has no
        // permission gate at all, so only an official, signed one is accepted (anything else is refused here
        // and again at install), while a JS plugin's declared permissions are shown and confirmed here rather than after install.
        api.MapPost("/plugins/install/browse", async (IUiDialogService dialogs, PluginManager plugins, PluginInstallSelection selection) =>
        {
            var sourceDir = await dialogs.BrowseForFolderAsync("Choose the plugin folder (must contain plugin.json)");
            if (sourceDir is null) return Results.Json(new { canceled = true });

            if (!File.Exists(Path.Combine(sourceDir, "plugin.json")))
                return ApiResults.BadRequest($"No plugin.json in the selected folder: {sourceDir}");

            try
            {
                var manifest = PluginManager.PeekManifest(sourceDir);
                if (plugins.CheckInstallTrust(sourceDir, manifest) is { } refusal)
                    return ApiResults.BadRequest(refusal);
                selection.Set(sourceDir);
                return ApiResults.Json(new {
                    canceled = false, path = sourceDir, id = manifest.Id, name = manifest.Name,
                    kind = manifest.Kind.ToString().ToLowerInvariant(), permissions = manifest.Permissions ?? [],
                });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return ApiResults.BadRequest(ex.Message);
            }
        });

        api.MapPost("/plugins/install/confirm", async (PluginInstallConfirmRequest body, PluginManager plugins, PluginInstallSelection selection) =>
        {
            // Only the folder the person picked in the dialog can be installed, never a path the request names.
            if (selection.Take(body.Path) is not { } folder)
                return ApiResults.BadRequest("Choose the plugin folder again before installing.");
            try
            {
                var result = await plugins.InstallFromFolderAsync(folder);
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

        // Switches one approved permission of a JavaScript plugin off or on; the plugin restarts without it.
        api.MapPut("/plugins/{id}/permissions", async (string id, PluginPermissionChange change, PluginManager plugins) =>
            string.IsNullOrWhiteSpace(change.Permission) ? Results.BadRequest()
            : await plugins.SetPermissionAsync(id, change.Permission, change.Enabled) is { } info ? ApiResults.Json(info) : Results.NotFound());

        api.MapPost("/plugins/{id}/reload", async (string id, PluginManager plugins) =>
            await plugins.ReloadAsync(id) is { } info ? ApiResults.Json(info) : Results.NotFound());

        // Unloads the plugin (its actions, variables and status items disappear immediately) and deletes its
        // folder. Plugin DLLs are loaded from memory so they are not locked; if a file is still in use anyway
        // the folder is marked and removed at the next start ("pending").
        api.MapDelete("/plugins/{id}", async (string id, PluginManager plugins) =>
            await plugins.UninstallAsync(id) is { } result
                ? Results.Json(new { removed = result.Removed, pending = result.Pending })
                : Results.NotFound());

        return api.MapPluginSettingsApi().MapPluginTreeApi();
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
                return ApiResults.Json(RedactPasswords(page.Fields, page.Load()));

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
                RestoreUnchangedPasswords(page.Fields, values, page.Load);
                try { page.Save(values); }
                catch (InvalidOperationException ex) { return ApiResults.BadRequest(ex.Message); }
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

    /// <summary><paramref name="values"/> (a form's current values, from a settings page or a plugin tree item) with
    /// every top-level <see cref="SettingFieldKind.Password"/> field blanked out, so the editor never receives an
    /// existing password back over the API — only what the person just typed. Fields nested in a
    /// <see cref="SettingFieldKind.List"/> row are not covered; no shipped plugin nests a password there yet.</summary>
    internal static JsonObject RedactPasswords(IReadOnlyList<SettingField> fields, JsonObject values)
    {
        foreach (var field in fields)
            if (field.Kind == SettingFieldKind.Password && values.ContainsKey(field.Key))
                values[field.Key] = "";
        return values;
    }

    /// <summary>An empty <see cref="SettingFieldKind.Password"/> field means "leave as is" (the editor never
    /// shows the real value to blank it against, see <see cref="RedactPasswords"/>), so it is restored from the
    /// stored values (<paramref name="loadStored"/>) before the form is saved. A non-empty value is a real change
    /// and passes through.</summary>
    internal static void RestoreUnchangedPasswords(IReadOnlyList<SettingField> fields, JsonObject values, Func<JsonObject> loadStored)
    {
        List<SettingField>? passwordFields = null;
        foreach (var field in fields)
            if (field.Kind == SettingFieldKind.Password && values[field.Key] is JsonValue v && v.GetValueKind() == JsonValueKind.String && v.GetValue<string>().Length == 0)
                (passwordFields ??= []).Add(field);
        if (passwordFields is null) return;

        var stored = loadStored();
        foreach (var field in passwordFields)
            values[field.Key] = stored[field.Key]?.DeepClone();
    }

    private sealed record SettingsCommandRequest(string? Command, JsonObject? Values);

    private sealed record PluginInstallConfirmRequest(string Path);

    private sealed record PluginPermissionChange(string Permission, bool Enabled);
}
