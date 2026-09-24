using System.Text.Json.Nodes;

namespace MacroGrid.Core.Sessions;

/// <summary>
/// Turns "the profile this client already has" and "the profile it should have now" into the smallest
/// <c>layout.patch</c> payload: only pages whose settings changed, only widgets that were added or edited, and
/// the id order of everything so the client can rebuild the lists. Works on the serialized JSON, so it
/// compares exactly what the client would see (including asset references), whatever the widget type.
/// </summary>
public static class LayoutDiff
{
    /// <param name="Patch">The <c>layout.patch</c> payload, or null when nothing the client renders changed.</param>
    /// <param name="ChangedWidgetIds">Widgets that were added, edited or removed — the client drops their cached
    /// live state and the server re-sends it.</param>
    public sealed record Result(JsonObject? Patch, HashSet<string> ChangedWidgetIds);

    public static Result Compute(JsonObject oldProfile, JsonObject newProfile, string currentPageId)
    {
        var changedWidgetIds = new HashSet<string>();
        var oldPages = PagesById(oldProfile);
        var pagePatches = new JsonArray();
        var pageOrder = new JsonArray();
        var pageOrderChanged = false;

        var newPages = newProfile["pages"] as JsonArray ?? [];
        var oldOrder = (oldProfile["pages"] as JsonArray ?? []).Select(IdOf).ToList();

        var index = 0;
        foreach (var newPage in newPages.OfType<JsonObject>())
        {
            var pageId = IdOf(newPage);
            pageOrder.Add(pageId);
            if (index >= oldOrder.Count || oldOrder[index] != pageId) pageOrderChanged = true;
            index++;

            var newWidgets = newPage["widgets"] as JsonArray ?? [];
            var newIds = newWidgets.OfType<JsonObject>().Select(IdOf).ToList();

            if (!oldPages.TryGetValue(pageId, out var oldPage))
            {
                foreach (var id in newIds) changedWidgetIds.Add(id);
                pagePatches.Add(new JsonObject
                {
                    ["id"] = pageId,
                    ["meta"] = Meta(newPage),
                    ["order"] = ToArray(newIds),
                    ["widgets"] = newWidgets.DeepClone(),
                });
                continue;
            }

            var oldWidgetList = (oldPage["widgets"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            var oldIds = oldWidgetList.Select(IdOf).ToList();
            var oldWidgets = oldWidgetList.ToDictionary(IdOf);
            var upserts = new JsonArray();
            foreach (var widget in newWidgets.OfType<JsonObject>())
            {
                var id = IdOf(widget);
                if (oldWidgets.TryGetValue(id, out var before) && JsonNode.DeepEquals(before, widget)) continue;
                changedWidgetIds.Add(id);
                upserts.Add(widget.DeepClone());
            }

            foreach (var removed in oldIds.Except(newIds)) changedWidgetIds.Add(removed);

            var metaChanged = !JsonNode.DeepEquals(Meta(oldPage), Meta(newPage));
            var orderChanged = !oldIds.SequenceEqual(newIds);
            if (!metaChanged && !orderChanged && upserts.Count == 0) continue;

            var patch = new JsonObject { ["id"] = pageId, ["widgets"] = upserts };
            if (metaChanged) patch["meta"] = Meta(newPage);
            if (orderChanged) patch["order"] = ToArray(newIds);
            pagePatches.Add(patch);
        }

        // Removed pages: their widgets are gone from the client's list too.
        foreach (var (id, page) in oldPages)
        {
            if (newPages.OfType<JsonObject>().Any(p => IdOf(p) == id)) continue;
            pageOrderChanged = true;
            foreach (var widget in page["widgets"] as JsonArray ?? [])
                if (widget is JsonObject w) changedWidgetIds.Add(IdOf(w));
        }

        var nameChanged = !JsonNode.DeepEquals(oldProfile["name"], newProfile["name"]);
        if (pagePatches.Count == 0 && !pageOrderChanged && !nameChanged)
            return new Result(null, changedWidgetIds);

        var payload = new JsonObject
        {
            ["profileId"] = newProfile["id"]?.DeepClone(),
            ["pageId"] = currentPageId,
            ["pageOrder"] = pageOrder,
            ["pages"] = pagePatches,
        };
        if (nameChanged) payload["name"] = newProfile["name"]?.DeepClone();
        return new Result(payload, changedWidgetIds);
    }

    /// <summary>A page's own settings (size, gap, ...) — everything except its id and widget list.</summary>
    private static JsonObject Meta(JsonObject page)
    {
        var meta = new JsonObject();
        foreach (var (key, value) in page)
            if (key is not ("id" or "widgets"))
                meta[key] = value?.DeepClone();
        return meta;
    }

    private static Dictionary<string, JsonObject> PagesById(JsonObject profile) =>
        (profile["pages"] as JsonArray ?? []).OfType<JsonObject>().ToDictionary(IdOf);

    private static string IdOf(JsonNode? node) => node?["id"]?.GetValue<string>() ?? "";

    private static JsonArray ToArray(IEnumerable<string> ids) => new([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]);
}
