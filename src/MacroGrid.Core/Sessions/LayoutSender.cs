using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Plugin.Abstractions;
using MacroGrid.Protocol;
using MacroGrid.Core.Plugins.Widgets;
using MacroGrid.Core.Widgets;

namespace MacroGrid.Core.Sessions;

public enum LayoutSendKind
{
    /// <summary>The client already had exactly this layout; nothing was sent.</summary>
    Unchanged,
    Patch,
    Full,
}

/// <param name="ChangedWidgetIds">For a <see cref="LayoutSendKind.Patch"/>: widgets that were added, edited or
/// removed, whose live state the caller must reset and re-send. For a full layout, everything is reset anyway.</param>
public sealed record LayoutSendResult(LayoutSendKind Kind, IReadOnlySet<string> ChangedWidgetIds);

/// <summary>
/// The one place a profile layout goes out to a client. Depending on what the client announced in its
/// <c>hello</c> it gets large <c>data:</c> values as cached asset references, and profile edits as small
/// <c>layout.patch</c> messages; a client that announced nothing gets the plain full layout, as before.
/// </summary>
public sealed class LayoutSender(AssetStore assets, PluginWidgetCatalog? pluginWidgets = null)
{
    private static readonly IReadOnlySet<string> NoWidgets = new HashSet<string>();

    /// <summary>Sends the whole profile (first connect, profile switch, or whenever a patch is not possible).</summary>
    public async Task SendFullAsync(ClientSession session, Profile profile, string pageId, CancellationToken ct = default)
    {
        await session.LayoutLock.WaitAsync(ct);
        try { await SendFullCoreAsync(session, profile, pageId, ct); }
        finally { session.LayoutLock.Release(); }
    }

    private async Task SendFullCoreAsync(ClientSession session, Profile profile, string pageId, CancellationToken ct)
    {
        var node = Snapshot(session, profile);
        session.SentLayout = session.Supports(ClientCapabilities.LayoutPatch) ? (JsonObject)node.DeepClone() : null;
        await session.SendAsync(new Envelope(MessageTypes.LayoutFull, new JsonObject { ["profile"] = node, ["pageId"] = pageId }), ct);
    }

    /// <summary>Brings a client that is already showing this profile up to date after an edit: a patch of just
    /// the differences if the client supports it and has a baseline, the full layout otherwise.</summary>
    public async Task<LayoutSendResult> SendUpdateAsync(ClientSession session, Profile profile, string pageId, CancellationToken ct = default)
    {
        await session.LayoutLock.WaitAsync(ct);
        try { return await SendUpdateCoreAsync(session, profile, pageId, ct); }
        finally { session.LayoutLock.Release(); }
    }

    private async Task<LayoutSendResult> SendUpdateCoreAsync(ClientSession session, Profile profile, string pageId, CancellationToken ct)
    {
        if (!session.Supports(ClientCapabilities.LayoutPatch) || session.SentLayout is not { } baseline)
        {
            await SendFullCoreAsync(session, profile, pageId, ct);
            return new LayoutSendResult(LayoutSendKind.Full, NoWidgets);
        }

        var next = Snapshot(session, profile);
        var diff = LayoutDiff.Compute(baseline, next, pageId);
        session.SentLayout = next;
        if (diff.Patch is null)
            return new LayoutSendResult(LayoutSendKind.Unchanged, diff.ChangedWidgetIds);

        await session.SendAsync(new Envelope(MessageTypes.LayoutPatch, diff.Patch), ct);
        return new LayoutSendResult(LayoutSendKind.Patch, diff.ChangedWidgetIds);
    }

    /// <summary>A live style value for one client: a large <c>data:</c> value becomes an asset reference if that
    /// client understands them, everything else is returned unchanged.</summary>
    public Dictionary<string, string> ForClient(ClientSession session, Dictionary<string, string> style) =>
        session.Supports(ClientCapabilities.Assets)
            ? style.ToDictionary(kv => kv.Key, kv => assets.ExternalizeValue(kv.Value))
            : style;

    /// <summary>Answers an <c>asset.get</c>: one message per requested hash, with a null value for a hash the
    /// server no longer holds so the client does not wait for it.</summary>
    public async Task SendAssetsAsync(ClientSession session, IEnumerable<string> hashes, CancellationToken ct = default)
    {
        foreach (var hash in hashes.Distinct())
            await session.SendAsync(MessageTypes.Asset, new AssetMessage(hash, assets.Get(hash)), ct);
    }

    private JsonObject Snapshot(ClientSession session, Profile profile)
    {
        var node = JsonSerializer.SerializeToNode(profile, ProtocolJson.Options)!.AsObject();
        AddPluginWidgetRuntime(session, profile, node);
        if (session.Supports(ClientCapabilities.Assets))
            assets.Externalize(node);
        return node;
    }

    /// <summary>Gives every plugin widget of the layout its <c>runtime</c>: the script and image references, frame cap and options, or the reason it cannot run
    /// (a placeholder is drawn). Only for a client that announced the capability; the profile itself never carries it.</summary>
    private void AddPluginWidgetRuntime(ClientSession session, Profile profile, JsonObject node)
    {
        if (pluginWidgets is null || !session.Supports(ClientCapabilities.PluginWidgets) || node["pages"] is not JsonArray pages) return;
        for (var i = 0; i < profile.Pages.Count && i < pages.Count; i++)
        {
            if (pages[i]?["widgets"] is not JsonArray widgetNodes) continue;
            for (var j = 0; j < profile.Pages[i].Widgets.Count && j < widgetNodes.Count; j++)
            {
                var widget = profile.Pages[i].Widgets[j];
                if (widget.Type != WidgetTypes.PluginWidget || widgetNodes[j] is not JsonObject widgetNode) continue;
                var props = widgetNode["props"] as JsonObject ?? [];
                widgetNode["props"] = props;
                props[PluginWidgetProps.RuntimeKey] = BuildRuntime(widget);
            }
        }
    }

    private JsonObject BuildRuntime(Widget widget)
    {
        if (!PluginWidgetProps.TryRead(widget, out var pluginId, out var widgetId)) return new JsonObject { ["unavailable"] = "invalid" };
        var info = pluginWidgets!.Resolve(pluginId, widgetId, out var unavailable);
        return info is null ? new JsonObject { ["unavailable"] = unavailable ?? "missing" } : BuildRuntimeFor(info);
    }

    /// <summary>The <c>props.runtime</c> of a widget that can run: what a device (or the editor's preview) needs to start it.</summary>
    public static JsonObject BuildRuntimeFor(PluginWidgetInfo info)
    {
        var assetRefs = new JsonObject();
        foreach (var (name, reference) in info.AssetRefs) assetRefs[name] = reference;
        return new JsonObject
        {
            ["name"] = info.Widget.Manifest.Name,
            ["code"] = info.CodeRef,
            ["assets"] = assetRefs,
            ["fps"] = info.Widget.Fps,
            ["interactive"] = info.Widget.Manifest.Interactive,
            ["optionsOff"] = new JsonArray(info.Widget.OptionsOffByDefault.Select(o => (JsonNode?)JsonValue.Create(o)).ToArray()),
            ["options"] = new JsonArray(info.Widget.Options.Select(o => (JsonNode?)JsonValue.Create(o)).ToArray()),
            // Which settings are Variable fields: the renderer hands these to the widget as its bindings.
            ["variables"] = new JsonArray((info.Widget.Manifest.Settings ?? []).Where(f => f.Kind == SettingFieldKind.Variable).Select(f => (JsonNode?)JsonValue.Create(f.Key)).ToArray()),
            ["verified"] = info.Verified,
        };
    }
}
