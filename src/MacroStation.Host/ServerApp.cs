using System.Text.Json;
using System.Text.Json.Nodes;
using MacroStation.Core.Actions;
using MacroStation.Core.Devices;
using MacroStation.Core.Model;
using MacroStation.Core.Plugins;
using MacroStation.Core.Preferences;
using MacroStation.Core.Profiles;
using MacroStation.Core.Sessions;
using MacroStation.Core.Variables;
using MacroStation.Host.Logging;
using MacroStation.Plugin.Abstractions;
using MacroStation.Protocol;
using MacroStation.Windows.Audio;
using MacroStation.Windows.Input;
using MacroStation.Windows.Variables;
using MacroStation.Windows.Windows;

namespace MacroStation.Host;

internal static class ServerApp
{
    public const int Port = 9820;

    public static WebApplication Build(string[] args, IUiDialogService dialogs, IUiWindowService windows)
    {
        var dataDir = ProfileStore.DefaultDataDir;

        // A WinExe may be launched from any working directory (autostart, shortcut), so pin the content root.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(Port));

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddProvider(new FileLoggerProvider(Path.Combine(dataDir, "logs")));

        builder.Services.AddSingleton(new ProfileStore(dataDir));
        builder.Services.AddSingleton(new PreferencesStore(dataDir));
        builder.Services.AddSingleton<IInputService, WindowsInputService>();
        builder.Services.AddSingleton<IActionHandler, HotkeyAction>();
        builder.Services.AddSingleton<IActionHandler, TypeTextAction>();
        builder.Services.AddSingleton<IActionHandler, PageAction>();
        builder.Services.AddSingleton<IActionHandler, ProfileAction>();
        builder.Services.AddSingleton<IActionHandler, OpenAction>();
        builder.Services.AddSingleton<IActionHandler, OpenUrlAction>();
        builder.Services.AddSingleton<IActionHandler, DelayAction>();
        builder.Services.AddSingleton<IAudioService, WindowsAudioService>();
        builder.Services.AddSingleton<IActionHandler, SetVolumeAction>();
        builder.Services.AddSingleton<IActionHandler, SetMuteAction>();
        builder.Services.AddSingleton<IActionHandler, ToggleMuteAction>();
        builder.Services.AddSingleton<ActionDispatcher>();
        builder.Services.AddSingleton(dialogs);
        builder.Services.AddSingleton(windows);

        builder.Services.AddSingleton<VariableStore>();
        builder.Services.AddSingleton<IVariableStore>(sp => sp.GetRequiredService<VariableStore>());
        builder.Services.AddSingleton<SystemMetricsProvider>();
        builder.Services.AddSingleton<IVariableProvider>(sp => sp.GetRequiredService<SystemMetricsProvider>());
        builder.Services.AddSingleton<IVariableCatalogSource>(sp => sp.GetRequiredService<SystemMetricsProvider>());
        builder.Services.AddSingleton<SystemAudioProvider>();
        builder.Services.AddSingleton<IVariableProvider>(sp => sp.GetRequiredService<SystemAudioProvider>());
        builder.Services.AddSingleton<IVariableCatalogSource>(sp => sp.GetRequiredService<SystemAudioProvider>());
        var statusRegistry = new PluginStatusRegistry();
        builder.Services.AddSingleton(statusRegistry);
        statusRegistry.SetCore("server", $"Macro Station {ClientHub.ServerVersion}", StatusLevel.Idle, "server");

        var pluginsRoot = Path.Combine(dataDir, "plugins");

        builder.Services.AddSingleton<VariableCatalog>();
        builder.Services.AddSingleton<VariableProviderHost>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<VariableProviderHost>());
        // Registered after the provider host: loading a plugin starts its variable providers on that host.
        builder.Services.AddSingleton(sp => new PluginManager(pluginsRoot, ClientHub.ServerVersion,
            sp.GetRequiredService<PluginStatusRegistry>(), sp.GetRequiredService<ActionDispatcher>(),
            sp.GetRequiredService<VariableCatalog>(), sp.GetRequiredService<VariableProviderHost>(),
            sp.GetRequiredService<VariableStore>(), sp.GetRequiredService<ILogger<PluginManager>>()));
        builder.Services.AddHostedService(sp => sp.GetRequiredService<PluginManager>());

        builder.Services.AddSingleton(new DeviceStore(dataDir));
        builder.Services.AddSingleton<PairingService>();

        builder.Services.AddSingleton<SessionRegistry>();
        builder.Services.AddSingleton<ToggleStateStore>();
        builder.Services.AddSingleton<AssetStore>();
        builder.Services.AddSingleton<LayoutSender>();
        builder.Services.AddSingleton<WidgetStateService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WidgetStateService>());
        builder.Services.AddSingleton<IActiveWindowSource, ForegroundWindowMonitor>();
        builder.Services.AddSingleton<AutoProfileSwitcher>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<AutoProfileSwitcher>());
        builder.Services.AddSingleton<ClientHub>();

        var app = builder.Build();

        var sessionRegistry = app.Services.GetRequiredService<SessionRegistry>();
        void UpdateDeviceStatus() => statusRegistry.SetCore("devices", $"{sessionRegistry.All.Count} cihaz",
            sessionRegistry.All.Count > 0 ? StatusLevel.Ok : StatusLevel.Idle, "smartphone");
        sessionRegistry.Changed += UpdateDeviceStatus;
        UpdateDeviceStatus();

        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
        app.Map("/ws", async (HttpContext http, ClientHub hub) =>
        {
            if (!http.WebSockets.IsWebSocketRequest)
                return Results.BadRequest("WebSocket bekleniyor.");

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            var remote = http.Connection.RemoteIpAddress?.ToString() ?? "?";
            await hub.HandleAsync(socket, remote, http.RequestAborted);
            return Results.Empty;
        });

        // Same disk-cache trap as the HTML files below, but for the editor's own REST calls: without
        // this, a GET right after a successful PUT/POST could return a stale cached body, making a
        // save look like it silently "sometimes doesn't work" when it actually did.
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
                ctx.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            await next();
        });

        MapEditorApi(app);

        // Temporary test client (wwwroot/index.html) and the built editor (wwwroot/editor/) until the Capacitor client exists.
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            // Vite's JS/CSS filenames are content-hashed (index-<hash>.js), so they can cache forever —
            // but the *.html entry documents are not, and the editor's own WebView2 has a persistent
            // disk cache across app restarts. Without this, rebuilding the editor and restarting the
            // server still shows a stale bundle until the user manually hard-refreshes.
            OnPrepareResponse = ctx =>
            {
                if (ctx.File.Name.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    ctx.Context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            },
        });

        return app;
    }

    /// <summary>
    /// Editor-only HTTP API (profile CRUD, action catalog, a one-shot variable snapshot for preview).
    /// No auth yet: like the WebSocket endpoint, this trusts anything on the LAN until Stage 5 (pairing) covers it too.
    /// </summary>
    private static void MapEditorApi(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/profiles", (ProfileStore profiles) =>
            profiles.All.Select(p => new { p.Id, p.Name }));

        api.MapGet("/profiles/{id}", (string id, ProfileStore profiles) =>
            profiles.Get(id) is { } profile ? Results.Json(profile, ProtocolJson.Options) : Results.NotFound());

        api.MapPost("/profiles", (ProfileStore profiles) =>
        {
            var profile = new Profile { Name = "Yeni Profil", Pages = [new Page { Name = "Sayfa 1", Cols = 4, Rows = 3 }] };
            profiles.Save(profile);
            return Results.Json(profile, ProtocolJson.Options);
        });

        api.MapPut("/profiles/{id}", async (string id, HttpRequest request, ProfileStore profiles, WidgetStateService widgetState) =>
        {
            Profile? profile;
            try
            {
                profile = await JsonSerializer.DeserializeAsync<Profile>(request.Body, ProtocolJson.Options);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Geçersiz JSON." });
            }

            if (profile is null || profile.Id != id)
                return Results.BadRequest(new { error = "Profil id'si uyuşmuyor." });

            if (!ProfileValidator.Validate(profile, out var error))
                return Results.BadRequest(new { error });

            profiles.Save(profile);
            await widgetState.BroadcastProfileAsync(profile);
            return Results.NoContent();
        });

        api.MapDelete("/profiles/{id}", (string id, ProfileStore profiles) =>
            profiles.Delete(id) ? Results.NoContent() : Results.BadRequest(new { error = "Son profil silinemez." }));

        api.MapGet("/preferences", (PreferencesStore preferences) =>
            Results.Json(preferences.Get(), ProtocolJson.Options));

        api.MapPut("/preferences", async (HttpRequest request, PreferencesStore preferences) =>
        {
            AppPreferences? parsed;
            try
            {
                parsed = await JsonSerializer.DeserializeAsync<AppPreferences>(request.Body, ProtocolJson.Options);
            }
            catch (JsonException)
            {
                return Results.BadRequest(new { error = "Geçersiz JSON." });
            }
            if (parsed is null)
                return Results.BadRequest(new { error = "Geçersiz JSON." });

            preferences.Save(parsed);
            return Results.NoContent();
        });

        api.MapGet("/actions", (ActionDispatcher dispatcher, PluginManager plugins) =>
            dispatcher.Handlers.Select(h =>
            {
                var descriptor = h as IActionDescriptor;
                return new
                {
                    h.Type,
                    h.DisplayName,
                    Category = descriptor?.Category ?? "Diğer",
                    Description = descriptor?.Description,
                    Icon = descriptor?.Icon,
                    PluginId = plugins.GetActionPluginId(h.Type),
                    Fields = descriptor is { Fields.Count: > 0 } ? descriptor.Fields : null,
                };
            }).OrderBy(a => a.DisplayName));

        api.MapPost("/actions/{type}/options/{sourceId}", async (string type, string sourceId, HttpRequest request, ActionDispatcher dispatcher) =>
        {
            var handler = dispatcher.Handlers.FirstOrDefault(h => h.Type == type);
            if (handler is not IOptionsSource optionsSource)
                return Results.Json(new OptionsResult([], "Bu aksiyon dinamik seçenek sağlamıyor"));

            JsonObject? currentValues;
            try { currentValues = await JsonSerializer.DeserializeAsync<JsonObject>(request.Body, ProtocolJson.Options); }
            catch (JsonException) { return Results.BadRequest(new { error = "Geçersiz JSON." }); }

            try
            {
                var result = await optionsSource.GetOptionsAsync(sourceId, currentValues ?? [], request.HttpContext.RequestAborted);
                return Results.Json(result, ProtocolJson.Options);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new OptionsResult([], ex.Message));
            }
        });

        api.MapGet("/variables/snapshot", (VariableStore variables) =>
            Results.Json(variables.Snapshot(), ProtocolJson.Options));

        api.MapGet("/variables/catalog", (VariableCatalog catalog) => catalog.All);

        api.MapPost("/browse/executable", async (IUiDialogService dialogs) =>
        {
            var path = await dialogs.BrowseForExecutableAsync();
            return Results.Json(new { path });
        });

        api.MapPost("/browse/import-profile", async (IUiDialogService dialogs) =>
        {
            var (path, content) = await dialogs.OpenJsonFileAsync("Profil içe aktar");
            return Results.Json(new { path, content });
        });

        api.MapPost("/browse/export-profile", async (HttpRequest request, IUiDialogService dialogs) =>
        {
            ExportProfileRequest? body;
            try { body = await JsonSerializer.DeserializeAsync<ExportProfileRequest>(request.Body, ProtocolJson.Options); }
            catch (JsonException) { return Results.BadRequest(new { error = "Geçersiz JSON." }); }
            if (body is null) return Results.BadRequest(new { error = "Geçersiz JSON." });
            var path = await dialogs.SaveJsonFileAsync("Profili dışa aktar", body.FileName, body.Content);
            return Results.Json(new { path });
        });

        api.MapGet("/plugins", (PluginManager plugins) => plugins.Plugins);

        api.MapGet("/icon-packs", (PluginManager plugins) =>
            plugins.IconPacks.Select(p => new { p.Id, p.DisplayName, Icons = p.IconNames }));

        api.MapGet("/icon-packs/{packId}/{iconName}", (string packId, string iconName, PluginManager plugins) =>
        {
            var pack = plugins.IconPacks.FirstOrDefault(p => p.Id == packId);
            var svg = pack?.GetIconSvg(iconName);
            return svg is null ? Results.NotFound() : Results.Text(svg, "image/svg+xml");
        });

        api.MapPost("/plugins/install", async (IUiDialogService dialogs, PluginManager plugins) =>
        {
            var sourceDir = await dialogs.BrowseForFolderAsync("Choose the plugin folder (must contain plugin.json)");
            if (sourceDir is null) return Results.Json(new { installed = false, canceled = true });

            if (!File.Exists(Path.Combine(sourceDir, "plugin.json")))
                return Results.BadRequest(new { error = $"No plugin.json in the selected folder: {sourceDir}" });

            try
            {
                var result = await plugins.InstallFromFolderAsync(sourceDir);
                return Results.Json(new { installed = true, id = result.Id, name = result.Name, status = result.Plugin.Status, detail = result.Plugin.Detail }, ProtocolJson.Options);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapPost("/plugins/{id}/reload", async (string id, PluginManager plugins) =>
            await plugins.ReloadAsync(id) is { } info ? Results.Json(info, ProtocolJson.Options) : Results.NotFound());

        // Unloads the plugin (its actions, variables and status items disappear immediately) and deletes its
        // folder. Plugin DLLs are loaded from memory so they are not locked; if a file is still in use anyway
        // the folder is marked and removed at the next start ("pending").
        api.MapDelete("/plugins/{id}", async (string id, PluginManager plugins) =>
            await plugins.UninstallAsync(id) is { } result
                ? Results.Json(new { removed = result.Removed, pending = result.Pending })
                : Results.NotFound());

        // A registered IPluginSettingsPage (schema-driven form) takes precedence; a plugin without one
        // falls back to the raw settings.json passthrough it always had (see docs/plugin-authoring.md
        // §"Ayarlar") so older plugins keep working unchanged.
        api.MapGet("/plugins/{id}/settings/schema", (string id, PluginManager plugins) =>
            plugins.GetSettingsPage(id) is { } page ? Results.Json(page.Fields, ProtocolJson.Options) : Results.NotFound());

        api.MapGet("/plugins/{id}/settings", (string id, PluginManager plugins) =>
        {
            if (plugins.GetSettingsPage(id) is { } page)
                return Results.Json(page.Load(), ProtocolJson.Options);

            var dir = plugins.GetPluginDir(id);
            if (dir is null) return Results.NotFound();
            var path = Path.Combine(dir, "settings.json");
            return File.Exists(path) ? Results.Text(File.ReadAllText(path), "application/json") : Results.NotFound();
        });

        api.MapPut("/plugins/{id}/settings", async (string id, HttpRequest request, PluginManager plugins) =>
        {
            if (plugins.GetSettingsPage(id) is { } page)
            {
                JsonObject? values;
                try { values = await JsonSerializer.DeserializeAsync<JsonObject>(request.Body, ProtocolJson.Options); }
                catch (JsonException) { return Results.BadRequest(new { error = "Geçersiz JSON." }); }
                if (values is null) return Results.BadRequest(new { error = "Geçersiz JSON." });
                page.Save(values);
                return Results.NoContent();
            }

            var dir = plugins.GetPluginDir(id);
            if (dir is null) return Results.NotFound();

            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            try { JsonDocument.Parse(body); }
            catch (JsonException) { return Results.BadRequest(new { error = "Geçersiz JSON." }); }

            await File.WriteAllTextAsync(Path.Combine(dir, "settings.json"), body);
            return Results.NoContent();
        });

        api.MapPost("/plugins/{id}/settings/options/{sourceId}", async (string id, string sourceId, HttpRequest request, PluginManager plugins) =>
        {
            if (plugins.GetSettingsPage(id) is not IOptionsSource optionsSource)
                return Results.Json(new OptionsResult([], "Bu plugin dinamik seçenek sağlamıyor"));

            JsonObject? currentValues;
            try { currentValues = await JsonSerializer.DeserializeAsync<JsonObject>(request.Body, ProtocolJson.Options); }
            catch (JsonException) { return Results.BadRequest(new { error = "Geçersiz JSON." }); }

            try
            {
                var result = await optionsSource.GetOptionsAsync(sourceId, currentValues ?? [], request.HttpContext.RequestAborted);
                return Results.Json(result, ProtocolJson.Options);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return Results.Json(new OptionsResult([], ex.Message));
            }
        });

        api.MapGet("/status", (PluginStatusRegistry statusRegistry) => statusRegistry.All);

        api.MapPost("/windows/preferences", async (IUiWindowService windows) =>
        {
            await windows.ShowToolWindowAsync("preferences", "Tercihler", $"http://localhost:{Port}/editor/?window=preferences", 640, 520);
            return Results.NoContent();
        });

        api.MapPost("/windows/plugins", async (IUiWindowService windows) =>
        {
            await windows.ShowToolWindowAsync("plugins", "Eklentiler", $"http://localhost:{Port}/editor/?window=plugins", 640, 520);
            return Results.NoContent();
        });

        api.MapPost("/windows/plugin-settings/{id}", async (string id, IUiWindowService windows, PluginManager plugins) =>
        {
            var name = plugins.Plugins.FirstOrDefault(p => p.Id == id)?.Name ?? id;
            await windows.ShowToolWindowAsync($"plugin-settings-{id}", name,
                $"http://localhost:{Port}/editor/?window=plugin-settings&id={Uri.EscapeDataString(id)}", 520, 560);
            return Results.NoContent();
        });

        api.MapPost("/windows/help", async (IUiWindowService windows) =>
        {
            await windows.ShowToolWindowAsync("help", "Yardım", $"http://localhost:{Port}/editor/?window=help", 640, 520);
            return Results.NoContent();
        });

        api.MapPost("/windows/pairing", async (IUiWindowService windows) =>
        {
            await windows.ShowToolWindowAsync("pairing", "Eşleştirme", $"http://localhost:{Port}/editor/?window=pairing", 760, 560);
            return Results.NoContent();
        });

        api.MapGet("/pairing/pin", (PairingService pairing) => new { pin = pairing.CurrentPin });

        api.MapPost("/pairing/pin/regenerate", (PairingService pairing) => new { pin = pairing.Regenerate() });

        api.MapGet("/pairing/qr", (PairingService pairing) => BuildPairingQr(pairing.CurrentPin, pairing.ExpiresAt));

        api.MapPost("/pairing/qr/regenerate", (PairingService pairing) => BuildPairingQr(pairing.Regenerate(), pairing.ExpiresAt));

        api.MapGet("/devices", (DeviceStore devices) =>
            devices.All.Select(d => new { d.Id, d.Name, d.PairedAt, d.LastSeenAt, d.AssignedProfileId, d.FollowActiveWindow, d.AutoSwitchLocked }));

        api.MapDelete("/devices/{id}", (string id, DeviceStore devices) =>
            devices.Revoke(id) ? Results.NoContent() : Results.NotFound());

        api.MapPut("/devices/{id}/profile", async (string id, HttpRequest request, DeviceStore devices) =>
        {
            AssignProfileRequest? body;
            try { body = await JsonSerializer.DeserializeAsync<AssignProfileRequest>(request.Body, ProtocolJson.Options); }
            catch (JsonException) { return Results.BadRequest(); }

            return devices.AssignProfile(id, body?.ProfileId) ? Results.NoContent() : Results.NotFound();
        });

        api.MapPut("/devices/{id}/follow-window", async (string id, HttpRequest request, DeviceStore devices, SessionRegistry sessions, AutoProfileSwitcher autoSwitcher) =>
        {
            FollowWindowRequest? body;
            try { body = await JsonSerializer.DeserializeAsync<FollowWindowRequest>(request.Body, ProtocolJson.Options); }
            catch (JsonException) { return Results.BadRequest(); }
            if (body is null) return Results.BadRequest();

            if (!devices.SetFollowActiveWindow(id, body.FollowActiveWindow)) return Results.NotFound();

            // Applies to the live session too, not just future connections — re-evaluate against the
            // current foreground window right away if it was just turned on.
            var session = sessions.All.FirstOrDefault(s => s.DeviceId == id);
            if (session is not null && body.FollowActiveWindow)
                await autoSwitcher.ReevaluateAsync(session);

            return Results.NoContent();
        });

        api.MapGet("/system/windows", (IActiveWindowSource windowSource) => windowSource.ListVisibleWindows());
    }

    private sealed record AssignProfileRequest(string? ProfileId);
    private sealed record FollowWindowRequest(bool FollowActiveWindow);

    /// <summary>
    /// The pairing QR's payload: a <c>macrostation://pair</c> deep-link URI carrying the LAN host, port and
    /// current PIN, so a client can decode it without agreeing on a bespoke delimited format. Uses the first
    /// LAN address <see cref="NetworkInfo.GetLanAddresses"/> reports (gateway-having adapters first); if the
    /// host has no LAN adapter up, <c>host</c> comes back empty and the editor should show a warning instead
    /// of a QR code, since a QR with no reachable host is useless.
    /// </summary>
    private static object BuildPairingQr(string pin, DateTimeOffset expiresAt)
    {
        var host = NetworkInfo.GetLanAddresses().FirstOrDefault()?.ToString() ?? "";
        var text = string.IsNullOrEmpty(host)
            ? ""
            : $"macrostation://pair?host={Uri.EscapeDataString(host)}&port={Port}&pin={pin}";
        return new { text, host, port = Port, pin, expiresAt };
    }
}

file sealed record ExportProfileRequest(string FileName, string Content);
