using MacroGrid.Core.Devices;
using MacroGrid.Core.Diagnostics;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Profiles;
using MacroGrid.Core.Sessions;
using MacroGrid.Host.Ui;
using MacroGrid.Host.Api;
using MacroGrid.Host.Logging;
using MacroGrid.Plugin.Abstractions;

namespace MacroGrid.Host;

/// <summary>Composition root of the embedded server: services, middleware, the WebSocket endpoint and the editor API.</summary>
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

        builder.Services
            .AddHostStores(dataDir)
            .AddBuiltInActions(dialogs, windows)
            .AddVariablesAndStatus()
            .AddPlugins(dataDir)
            .AddPluginDistribution(dataDir)
            .AddClientSessions(dataDir)
            .AddUpdates(dataDir);

        var app = builder.Build();

        TrackDeviceCount(app.Services);

        // A devices.json copied from another Windows user or PC cannot be decrypted here; say so instead of losing
        // the pairings silently.
        var deviceStore = app.Services.GetRequiredService<DeviceStore>();
        if (deviceStore.UnreadableOnLoad > 0)
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MacroGrid.Security").LogWarning(SecurityEvents.DeviceTokenUnreadable,
                "Security: {Count} paired device(s) dropped because their token cannot be decrypted by this Windows user on this PC; they have to pair again", deviceStore.UnreadableOnLoad);

        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(15) });
        app.Map("/ws", async (HttpContext http, ClientHub hub) =>
        {
            if (!http.WebSockets.IsWebSocketRequest)
                return Results.BadRequest("A WebSocket request is expected.");

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            var remote = http.Connection.RemoteIpAddress?.ToString() ?? "?";
            await hub.HandleAsync(socket, remote, http.RequestAborted);
            return Results.Empty;
        });

        // The editor API (profiles, plugin install and approval, the pairing PIN, ...) is for the editor in the
        // server's own window only. Phones and browser decks talk to /ws; nothing else on the network may
        // reach /api, otherwise anyone on the LAN could read the pairing PIN or approve a plugin's permissions.
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api") && !LoopbackGuard.IsLoopback(ctx.Connection.RemoteIpAddress))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsync("The editor API is only available on this computer.");
                return;
            }
            await next();
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

    /// <summary>Keeps the "devices" status item in step with the number of connected clients.</summary>
    private static void TrackDeviceCount(IServiceProvider services)
    {
        var statusRegistry = services.GetRequiredService<PluginStatusRegistry>();
        var sessionRegistry = services.GetRequiredService<SessionRegistry>();
        void UpdateDeviceStatus() => statusRegistry.SetCore("devices", $"{sessionRegistry.All.Count} devices",
            sessionRegistry.All.Count > 0 ? StatusLevel.Ok : StatusLevel.Idle, "smartphone");
        sessionRegistry.Changed += UpdateDeviceStatus;
        UpdateDeviceStatus();
    }

    /// <summary>
    /// Editor-only HTTP API. It is loopback-only (see the guard above), so phones and browser decks on the LAN cannot reach it.
    /// </summary>
    private static void MapEditorApi(WebApplication app)
    {
        app.MapGroup("/api")
            .MapProfileApi()
            .MapAppApi()
            .MapCatalogApi()
            .MapPluginApi()
            .MapPluginCatalogApi()
            .MapWindowApi()
            .MapDeviceApi()
            .MapUpdateApi();
    }
}
