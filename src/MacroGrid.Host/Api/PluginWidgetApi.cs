using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Core.Plugins;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Sessions;
using MacroGrid.Core.Variables;
using MacroGrid.Core.Widgets;
using MacroGrid.Plugin.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace MacroGrid.Host.Api;

/// <summary>
/// What the editor needs to offer and preview plugin widgets: the list for the Toolbox, a widget's script and images (the editor fetches the same
/// content-hashed assets a device does), and a request to a widget's plugin from the preview. The preview never runs an action and never sends a real
/// device id: its requests carry the device id "editor". Loopback-only, like the rest of <c>/api</c>.
/// </summary>
internal static class PluginWidgetApi
{
    /// <summary>The device id a request from the editor's preview carries.</summary>
    public const string EditorDeviceId = "editor";

    public static RouteGroupBuilder MapPluginWidgetApi(this RouteGroupBuilder api)
    {
        api.MapGet("/plugin-widgets", (PluginWidgetCatalog catalog, PluginLocalizer localizer) =>
            catalog.Available().Select(info => new
            {
                plugin = info.PluginId,
                pluginName = localizer.Translate(info.PluginId, info.PluginName),
                widget = info.Widget.Manifest.Id,
                name = localizer.Translate(info.PluginId, info.Widget.Manifest.Name),
                description = info.Widget.Manifest.Description is { } d ? localizer.Translate(info.PluginId, d) : null,
                category = info.Widget.Manifest.Category is { } c ? localizer.Translate(info.PluginId, c) : null,
                size = new { w = info.Widget.Size.W, h = info.Widget.Size.H },
                fps = info.Widget.Fps,
                interactive = info.Widget.Manifest.Interactive,
                options = info.Widget.Options,
                verified = info.Verified,
                settings = info.Widget.Manifest.Settings,
            }));

        // The runtime of one widget as a device would get it (script and images as asset references), for the editor's preview.
        api.MapGet("/plugin-widgets/{plugin}/{widget}/runtime", (string plugin, string widget, PluginWidgetCatalog catalog) =>
        {
            var info = catalog.Resolve(plugin, widget, out var unavailable);
            if (info is null) return ApiResults.Json(new { unavailable = unavailable ?? "missing" });
            return ApiResults.Json(LayoutSender.BuildRuntimeFor(info));
        });

        // One asset (a widget script or image) by its hash: the same store devices fetch from over the socket.
        api.MapGet("/plugin-widgets/assets/{hash}", (string hash, AssetStore assets) =>
            assets.Get(hash) is { } data ? Results.Text(data, "text/plain") : Results.NotFound());

        // A request from the preview to the widget's plugin.
        api.MapPost("/plugin-widgets/{plugin}/{widget}/request", async (string plugin, string widget, HttpRequest request, PluginWidgetCatalog catalog, IPluginWidgetHost host, CancellationToken ct) =>
        {
            var info = catalog.Resolve(plugin, widget, out _);
            if (info is null) return ApiResults.BadRequest("plugin_unavailable");
            var handler = host.GetWidgetHandler(plugin);
            if (handler is null) return ApiResults.BadRequest("not_allowed");

            var (valid, body) = await ApiResults.ReadJsonAsync<JsonObject>(request);
            if (!valid || body is null) return ApiResults.InvalidJson();
            if (body.ToJsonString().Length > PluginWidgetRouter.MaxRequestBytes) return ApiResults.BadRequest("too_large");

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(PluginWidgetRouter.HandlerTimeout);
            try
            {
                var message = new PluginWidgetMessage(widget, EditorDeviceId, "", "", body["settings"] as JsonObject ?? [], body["data"]?.DeepClone());
                var reply = await handler.OnWidgetMessageAsync(message, timeout.Token);
                return ApiResults.Json(new { data = reply });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ApiResults.BadRequest("timeout");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ApiResults.BadRequest("failed: " + (ex.Message.Length > 200 ? ex.Message[..200] : ex.Message));
            }
        });

        return api;
    }
}
