using System.Text.Json;
using System.Text.Json.Nodes;
using MacroGrid.Core.Model;
using MacroGrid.Protocol;

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
public sealed class LayoutSender(AssetStore assets)
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
        if (session.Supports(ClientCapabilities.Assets))
            assets.Externalize(node);
        return node;
    }
}
