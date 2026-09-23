using System.Text.Json;
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
        var pluginsRoot = Path.Combine(dataDir, "plugins");
        var pluginLoad = PluginLoader.LoadAll(pluginsRoot, ClientHub.ServerVersion,
            new FileLoggerProvider(Path.Combine(dataDir, "logs")).CreateLogger("Plugins"));
        builder.Services.AddSingleton(pluginLoad);
        foreach (var action in pluginLoad.Actions)
            builder.Services.AddSingleton<IActionHandler>(action);
        foreach (var provider in pluginLoad.VariableProviders)
        {
            builder.Services.AddSingleton<IVariableProvider>(provider);
            if (provider is IVariableCatalogSource catalogSource)
                builder.Services.AddSingleton<IVariableCatalogSource>(catalogSource);
        }

        builder.Services.AddSingleton<VariableCatalog>();
        builder.Services.AddHostedService<VariableProviderHost>();

        builder.Services.AddSingleton(new DeviceStore(dataDir));
        builder.Services.AddSingleton<PairingService>();

        builder.Services.AddSingleton<SessionRegistry>();
        builder.Services.AddSingleton<ToggleStateStore>();
        builder.Services.AddSingleton<WidgetStateService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<WidgetStateService>());
        builder.Services.AddSingleton<ClientHub>();

        var app = builder.Build();

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

        MapEditorApi(app, pluginsRoot);

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
    private static void MapEditorApi(WebApplication app, string pluginsRoot)
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

        api.MapGet("/actions", (ActionDispatcher dispatcher) =>
            dispatcher.Handlers.Select(h => new { h.Type, h.DisplayName }).OrderBy(a => a.DisplayName));

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

        api.MapGet("/plugins", (PluginLoadResult pluginLoad) => pluginLoad.Plugins);

        api.MapPost("/plugins/install", async (IUiDialogService dialogs) =>
        {
            var sourceDir = await dialogs.BrowseForFolderAsync("Plugin klasörünü seç (plugin.json içermeli)");
            if (sourceDir is null) return Results.Json(new { installed = false, canceled = true });

            var manifestPath = Path.Combine(sourceDir, "plugin.json");
            if (!File.Exists(manifestPath))
                return Results.BadRequest(new { error = $"Seçilen klasörde plugin.json yok: {sourceDir}" });

            PluginManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new JsonException("plugin.json boş");
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                return Results.BadRequest(new { error = $"plugin.json ayrıştırılamadı: {ex.Message}" });
            }

            Directory.CreateDirectory(pluginsRoot);
            if (Directory.EnumerateDirectories(pluginsRoot)
                .Any(d => File.Exists(Path.Combine(d, "plugin.json"))
                    && JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(Path.Combine(d, "plugin.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))?.Id == manifest.Id))
            {
                return Results.BadRequest(new { error = $"'{manifest.Id}' id'li bir plugin zaten yüklü." });
            }

            var destDir = Path.Combine(pluginsRoot, manifest.Id);
            if (Directory.Exists(destDir)) Directory.Delete(destDir, recursive: true);
            CopyDirectory(sourceDir, destDir);

            return Results.Json(new { installed = true, id = manifest.Id, name = manifest.Name, requiresRestart = true });
        });

        // Generic raw-JSON passthrough to a plugin's own settings.json (see docs/plugin-authoring.md §"Ayarlar" —
        // the host has no per-plugin settings schema/UI, plugins own their settings file under their DataDirectory).
        // The editor builds whatever form makes sense per plugin (e.g. OBS's host/port/password) on top of this.
        api.MapGet("/plugins/{id}/settings", (string id) =>
        {
            var dir = ResolvePluginDir(pluginsRoot, id);
            if (dir is null) return Results.NotFound();
            var path = Path.Combine(dir, "settings.json");
            return File.Exists(path) ? Results.Text(File.ReadAllText(path), "application/json") : Results.NotFound();
        });

        api.MapPut("/plugins/{id}/settings", async (string id, HttpRequest request) =>
        {
            var dir = ResolvePluginDir(pluginsRoot, id);
            if (dir is null) return Results.NotFound();

            using var reader = new StreamReader(request.Body);
            var body = await reader.ReadToEndAsync();
            try { JsonDocument.Parse(body); }
            catch (JsonException) { return Results.BadRequest(new { error = "Geçersiz JSON." }); }

            await File.WriteAllTextAsync(Path.Combine(dir, "settings.json"), body);
            return Results.NoContent();
        });

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
            devices.All.Select(d => new { d.Id, d.Name, d.PairedAt, d.LastSeenAt, d.AssignedProfileId }));

        api.MapDelete("/devices/{id}", (string id, DeviceStore devices) =>
            devices.Revoke(id) ? Results.NoContent() : Results.NotFound());

        api.MapPut("/devices/{id}/profile", async (string id, HttpRequest request, DeviceStore devices) =>
        {
            AssignProfileRequest? body;
            try { body = await JsonSerializer.DeserializeAsync<AssignProfileRequest>(request.Body, ProtocolJson.Options); }
            catch (JsonException) { return Results.BadRequest(); }

            return devices.AssignProfile(id, body?.ProfileId) ? Results.NoContent() : Results.NotFound();
        });
    }

    private sealed record AssignProfileRequest(string? ProfileId);

    /// <summary>Resolves a plugin id from a URL segment to its install folder, rejecting anything that
    /// isn't a plain single path segment (no traversal) and any id that isn't actually installed.</summary>
    private static string? ResolvePluginDir(string pluginsRoot, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Contains("..") || id.Contains('/') || id.Contains('\\'))
            return null;
        var dir = Path.Combine(pluginsRoot, id);
        return Directory.Exists(dir) ? dir : null;
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(sourceDir))
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
    }

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
