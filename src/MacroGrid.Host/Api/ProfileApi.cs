using Microsoft.AspNetCore.Routing;
using MacroGrid.Core;
using MacroGrid.Core.Actions;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Protocol;
using MacroGrid.Windows.Autostart;
using MacroGrid.Windows.Windows;

namespace MacroGrid.Host.Api;

/// <summary>Editor endpoints for profiles and profile import/export.</summary>
internal static class ProfileApi
{
    public static RouteGroupBuilder MapProfileApi(this RouteGroupBuilder api)
    {
        api.MapGet("/profiles", (ProfileStore profiles) =>
            profiles.All.Select(p => new { p.Id, p.Name }));

        api.MapGet("/profiles/{id}", (string id, ProfileStore profiles) =>
            profiles.Get(id) is { } profile ? ApiResults.Json(profile) : Results.NotFound());

        api.MapPost("/profiles", (ProfileStore profiles) =>
        {
            var profile = new Profile { Name = AppLanguage.Pick("New profile", "Yeni Profil"), Pages = [new Page { Name = AppLanguage.Pick("Page 1", "Sayfa 1"), Cols = 4, Rows = 3 }] };
            profiles.Save(profile);
            return ApiResults.Json(profile);
        });

        api.MapPut("/profiles/{id}", async (string id, HttpRequest request, ProfileStore profiles, WidgetStateService widgetState) =>
        {
            var (valid, profile) = await ApiResults.ReadJsonAsync<Profile>(request);
            if (!valid)
                return ApiResults.InvalidJson();

            if (profile is null || profile.Id != id)
                return ApiResults.BadRequest("The profile id does not match.");

            if (!ProfileValidator.Validate(profile, out var error))
                return ApiResults.BadRequest(error);

            profiles.Save(profile);
            await widgetState.BroadcastProfileAsync(profile);
            return Results.NoContent();
        });

        api.MapDelete("/profiles/{id}", (string id, ProfileStore profiles) =>
            profiles.Delete(id) ? Results.NoContent() : ApiResults.BadRequest("The last profile cannot be deleted."));

        // .msprofile (a zip with the profile and a manifest naming the plugins it needs) or a plain profile JSON file.
        api.MapPost("/browse/import-profile", async (IUiDialogService dialogs, PluginManager plugins, ActionDispatcher dispatcher) =>
        {
            var (path, bytes) = await dialogs.OpenFileAsync("Import profile", "Macro Grid profile (*.msprofile;*.json)|*.msprofile;*.json");
            if (bytes is null) return Results.Json(new { path = (string?)null });

            ProfilePackageContent package;
            try { package = ProfilePackage.Read(bytes); }
            catch (InvalidDataException ex) { return ApiResults.BadRequest(ex.Message); }

            var missing = plugins.MissingPlugins(package.Manifest);
            var known = dispatcher.Handlers.Select(h => h.Type).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unknownTypes = ProfilePackage.ActionTypes(package.Profile).Where(t => !known.Contains(t)).ToList();
            return ApiResults.Json(new { path, profile = package.Profile, missingPlugins = missing, unknownActionTypes = unknownTypes });
        });

        api.MapPost("/browse/export-profile", async (HttpRequest request, IUiDialogService dialogs, PluginManager plugins) =>
        {
            var (valid, profile) = await ApiResults.ReadJsonAsync<Profile>(request);
            if (!valid || profile is null) return ApiResults.InvalidJson();

            var manifest = new ProfilePackageManifest(
                ProfilePackage.CurrentFormatVersion, profile.Name, DateTimeOffset.UtcNow, ClientHub.ServerVersion,
                plugins.DescribeRequiredPlugins(ProfilePackage.ActionTypes(profile)));
            var invalid = Path.GetInvalidFileNameChars();
            var fileName = string.Concat(profile.Name.Select(c => invalid.Contains(c) ? '_' : c)).Trim();
            var path = await dialogs.SaveFileAsync("Export profile", (fileName.Length > 0 ? fileName : "profile") + ProfilePackage.Extension,
                "Macro Grid profile (*.msprofile)|*.msprofile", "msprofile", ProfilePackage.Write(profile, manifest));
            return Results.Json(new { path });
        });

        return api;
    }
}
